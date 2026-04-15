// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.Auth;

/// <summary>
/// 认证服务实现。协调 OAuth 流程、Token 存储和会话管理。
/// </summary>
internal sealed class AuthService : IAuthService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<AuthService>();
    private readonly EpicOAuthHandler _oauthHandler;
    private readonly ITokenStore _tokenStore;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private TokenPair? _currentTokens;
    private AuthUserInfo? _currentUser;

    public bool IsAuthenticated
    {
        get
        {
            lock (_lock)
            {
                return _currentTokens is not null && _currentTokens.ExpiresAt > DateTime.UtcNow;
            }
        }
    }

    public AuthUserInfo? CurrentUser
    {
        get
        {
            lock (_lock)
            {
                return _currentUser;
            }
        }
    }

    public event Action<SessionExpiredEvent>? SessionExpired;

    public AuthService(EpicOAuthHandler oauthHandler, ITokenStore tokenStore)
    {
        _oauthHandler = oauthHandler;
        _tokenStore = tokenStore;
    }

    public async Task<Result<AuthUserInfo>> LoginAsync(CancellationToken ct = default)
    {
        _logger.Information("开始 OAuth 登录流程");

        // 1. 启动 OAuth 授权流程
        var tokenResult = await _oauthHandler.AuthorizeAsync(ct);
        if (!tokenResult.IsSuccess)
            return Result.Fail<AuthUserInfo>(tokenResult.Error!);

        var tokens = tokenResult.Value!;

        // 2. 获取用户信息
        var userResult = await _oauthHandler.GetAccountInfoAsync(tokens.AccessToken, tokens.AccountId, ct);
        AuthUserInfo userInfo;

        if (userResult.IsSuccess)
        {
            userInfo = userResult.Value!;
        }
        else
        {
            // 如果获取用户信息失败，用 Token 里的基本信息
            userInfo = new AuthUserInfo
            {
                AccountId = tokens.AccountId,
                DisplayName = tokens.DisplayName,
                Email = string.Empty,
            };
            _logger.Warning("获取详细用户信息失败，使用 Token 中的基本信息");
        }

        // 3. 存储 Token
        await _tokenStore.SaveTokensAsync(tokens, ct);

        // 4. 更新内存状态
        lock (_lock)
        {
            _currentTokens = tokens;
            _currentUser = userInfo;
        }

        _logger.Information("登录成功 | AccountId={AccountId} | DisplayName={Name}",
            userInfo.AccountId, userInfo.DisplayName);
        return Result.Ok(userInfo);
    }

    public async Task<Result> LogoutAsync(CancellationToken ct = default)
    {
        _logger.Information("开始登出流程");

        TokenPair? tokens;
        lock (_lock)
        {
            tokens = _currentTokens;
        }

        // 1. 撤销远程 Token（最佳努力）
        if (tokens is not null)
        {
            await _oauthHandler.RevokeTokenAsync(tokens.AccessToken, ct);
        }

        // 2. 清除本地存储
        await _tokenStore.ClearAsync(ct);

        // 3. 清除内存状态
        lock (_lock)
        {
            _currentTokens = null;
            _currentUser = null;
        }

        // 4. 发布会话过期事件
        SessionExpired?.Invoke(new SessionExpiredEvent("用户主动登出"));

        _logger.Information("登出完成");
        return Result.Ok();
    }

    public async Task<Result<string>> GetAccessTokenAsync(CancellationToken ct = default)
    {
        TokenPair? tokens;
        lock (_lock)
        {
            tokens = _currentTokens;
        }

        if (tokens is null)
        {
            return Result.Fail<string>(new Error
            {
                Code = "AUTH_NOT_AUTHENTICATED",
                UserMessage = "未登录，请先登录",
                TechnicalMessage = "No token pair available",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        // 检查是否需要刷新（提前 5 分钟）
        if (tokens.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            return Result.Ok(tokens.AccessToken);
        }

        // 需要刷新 — 用 SemaphoreSlim 防止并发刷新竞态
        await _refreshLock.WaitAsync(ct);
        try
        {
            // Double-check: 另一个线程可能已经刷新完成
            lock (_lock)
            {
                tokens = _currentTokens;
            }

            if (tokens is null)
            {
                return Result.Fail<string>(new Error
                {
                    Code = "AUTH_NOT_AUTHENTICATED",
                    UserMessage = "未登录，请先登录",
                    TechnicalMessage = "Session cleared during refresh",
                    CanRetry = false,
                    Severity = ErrorSeverity.Warning,
                });
            }

            if (tokens.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
            {
                return Result.Ok(tokens.AccessToken);
            }

            _logger.Debug("Access Token 即将过期，主动刷新");
            var refreshResult = await _oauthHandler.RefreshTokenAsync(tokens.RefreshToken, ct);

            if (!refreshResult.IsSuccess)
            {
                // 刷新失败 → 会话过期
                lock (_lock)
                {
                    _currentTokens = null;
                    _currentUser = null;
                }

                await _tokenStore.ClearAsync(ct);
                SessionExpired?.Invoke(new SessionExpiredEvent("Token 刷新失败"));

                return Result.Fail<string>(refreshResult.Error!);
            }

            var newTokens = refreshResult.Value!;
            await _tokenStore.SaveTokensAsync(newTokens, ct);

            // 写回前检查：如果 Logout 已介入清空了 token，则不写回
            lock (_lock)
            {
                if (_currentTokens is not null)
                {
                    _currentTokens = newTokens;
                }
                else
                {
                    _logger.Warning("刷新期间检测到登出操作，丢弃刷新结果");
                    return Result.Fail<string>(new Error
                    {
                        Code = "AUTH_LOGGED_OUT_DURING_REFRESH",
                        UserMessage = "操作期间已登出",
                        TechnicalMessage = "Logout occurred during token refresh",
                        CanRetry = false,
                        Severity = ErrorSeverity.Warning,
                    });
                }
            }

            return Result.Ok(newTokens.AccessToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<Result<AuthUserInfo>> TryRestoreSessionAsync(CancellationToken ct = default)
    {
        _logger.Debug("尝试恢复会话");

        // 1. 从存储加载 Token
        var tokens = await _tokenStore.LoadTokensAsync(ct);
        if (tokens is null)
        {
            _logger.Debug("未找到缓存的 Token");
            return Result.Fail<AuthUserInfo>(new Error
            {
                Code = "AUTH_NO_CACHED_SESSION",
                UserMessage = "无缓存会话",
                TechnicalMessage = "No tokens found in store",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        // 2. 检查 access_token 是否还有效
        if (tokens.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            // Token 仍有效，直接恢复
            return await RestoreWithTokens(tokens, ct);
        }

        // 3. Token 已过期，尝试刷新
        if (string.IsNullOrEmpty(tokens.RefreshToken))
        {
            await _tokenStore.ClearAsync(ct);
            return Result.Fail<AuthUserInfo>(new Error
            {
                Code = "AUTH_SESSION_EXPIRED",
                UserMessage = "会话已过期，请重新登录",
                TechnicalMessage = "Access token expired and no refresh token available",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        _logger.Debug("缓存 Token 已过期，尝试刷新");
        var refreshResult = await _oauthHandler.RefreshTokenAsync(tokens.RefreshToken, ct);

        if (!refreshResult.IsSuccess)
        {
            await _tokenStore.ClearAsync(ct);
            _logger.Warning("会话恢复失败：Token 刷新失败");
            return Result.Fail<AuthUserInfo>(new Error
            {
                Code = "AUTH_RESTORE_FAILED",
                UserMessage = "会话恢复失败，请重新登录",
                TechnicalMessage = "Token refresh failed during session restore",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        var newTokens = refreshResult.Value!;
        await _tokenStore.SaveTokensAsync(newTokens, ct);

        return await RestoreWithTokens(newTokens, ct);
    }

    private async Task<Result<AuthUserInfo>> RestoreWithTokens(TokenPair tokens, CancellationToken ct)
    {
        // 获取用户信息
        var userResult = await _oauthHandler.GetAccountInfoAsync(tokens.AccessToken, tokens.AccountId, ct);
        AuthUserInfo userInfo;

        if (userResult.IsSuccess)
        {
            userInfo = userResult.Value!;
        }
        else
        {
            userInfo = new AuthUserInfo
            {
                AccountId = tokens.AccountId,
                DisplayName = tokens.DisplayName,
                Email = string.Empty,
            };
        }

        lock (_lock)
        {
            _currentTokens = tokens;
            _currentUser = userInfo;
        }

        _logger.Information("会话已恢复 | AccountId={AccountId} | DisplayName={Name}",
            userInfo.AccountId, userInfo.DisplayName);
        return Result.Ok(userInfo);
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }
}
