// Copyright (c) Helsincy. All rights reserved.

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Shared;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Launcher.Infrastructure.Auth;

/// <summary>
/// Epic Games OAuth 2.0 处理器。
/// 管理本地 HTTP 回调监听、授权码交换、Token 刷新。
/// </summary>
internal sealed class EpicOAuthHandler
{
    private readonly ILogger _logger = Log.ForContext<EpicOAuthHandler>();
    private readonly IHttpClientFactory _httpClientFactory;

    // Epic Games OAuth 端点
    private const string AuthorizeUrl = "https://www.epicgames.com/id/authorize";
    private const string TokenUrl = "https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token";
    private const string AccountInfoUrl = "https://account-public-service-prod03.ol.epicgames.com/account/api/public/account";
    private const string RevokeUrl = "https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/sessions/kill";

    // 本地回调配置（端口自动分配避免冲突）
    private const string RedirectPath = "/callback";
    private const string LoopbackHost = "http://localhost";

    // 客户端凭据（从配置文件加载）
    private readonly string _clientId;
    private readonly string _clientSecret;

    public EpicOAuthHandler(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _clientId = configuration["EpicOAuth:ClientId"] ?? throw new InvalidOperationException("EpicOAuth:ClientId not configured");
        _clientSecret = configuration["EpicOAuth:ClientSecret"] ?? throw new InvalidOperationException("EpicOAuth:ClientSecret not configured");
    }

    /// <summary>
    /// 启动 OAuth 授权流程。打开系统浏览器并等待回调。
    /// </summary>
    public async Task<Result<TokenPair>> AuthorizeAsync(CancellationToken ct)
    {
        // 1. 启动本地 HTTP 监听器
        var (listener, redirectUri) = StartListener();
        _logger.Information("OAuth 回调监听已启动 | RedirectUri={Uri}", redirectUri);

        try
        {
            // 2. 构建授权 URL 并打开浏览器
            var authUrl = $"{AuthorizeUrl}?client_id={_clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}";
            OpenBrowser(authUrl);
            _logger.Debug("已打开浏览器进行 OAuth 授权");

            // 3. 等待回调获取 authorization_code
            var code = await WaitForCallbackAsync(listener, ct);
            if (code is null)
            {
                return Result.Fail<TokenPair>(new Error
                {
                    Code = "AUTH_CALLBACK_FAILED",
                    UserMessage = "未收到授权回调，登录已取消",
                    TechnicalMessage = "OAuth callback returned null code",
                    CanRetry = true,
                    Severity = ErrorSeverity.Warning,
                });
            }

            _logger.Debug("已收到授权码");

            // 4. 用授权码换取 Token
            return await ExchangeCodeAsync(code, redirectUri, ct);
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    /// <summary>
    /// 用 refresh_token 刷新 access_token
    /// </summary>
    public async Task<Result<TokenPair>> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = content,
            };
            AddClientAuth(request);

            using var httpClient = _httpClientFactory.CreateClient("EpicAuth");
            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Token 刷新失败 | StatusCode={Code}", response.StatusCode);
                return Result.Fail<TokenPair>(new Error
                {
                    Code = "AUTH_REFRESH_FAILED",
                    UserMessage = "Token 刷新失败，请重新登录",
                    TechnicalMessage = $"HTTP {(int)response.StatusCode}: {body}",
                    CanRetry = false,
                    Severity = ErrorSeverity.Warning,
                });
            }

            var tokenPair = ParseTokenResponse(body);
            _logger.Information("Token 已刷新 | ExpiresAt={ExpiresAt}", tokenPair.ExpiresAt);
            return Result.Ok(tokenPair);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "Token 刷新异常");
            return Result.Fail<TokenPair>(new Error
            {
                Code = "AUTH_REFRESH_EXCEPTION",
                UserMessage = "Token 刷新过程中出错",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }
    }

    /// <summary>
    /// 获取用户账户信息
    /// </summary>
    public async Task<Result<AuthUserInfo>> GetAccountInfoAsync(string accessToken, string accountId, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{AccountInfoUrl}/{accountId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var httpClient = _httpClientFactory.CreateClient("EpicAuth");
            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("获取账户信息失败 | StatusCode={Code}", response.StatusCode);
                return Result.Fail<AuthUserInfo>(new Error
                {
                    Code = "AUTH_ACCOUNT_INFO_FAILED",
                    UserMessage = "获取账户信息失败",
                    TechnicalMessage = $"HTTP {(int)response.StatusCode}: {body}",
                    CanRetry = true,
                    Severity = ErrorSeverity.Warning,
                });
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var userInfo = new AuthUserInfo
            {
                AccountId = root.GetProperty("id").GetString() ?? string.Empty,
                DisplayName = root.GetProperty("displayName").GetString() ?? string.Empty,
                Email = root.TryGetProperty("email", out var email) ? email.GetString() ?? string.Empty : string.Empty,
            };

            return Result.Ok(userInfo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "获取账户信息异常");
            return Result.Fail<AuthUserInfo>(new Error
            {
                Code = "AUTH_ACCOUNT_INFO_EXCEPTION",
                UserMessage = "获取账户信息过程中出错",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }
    }

    /// <summary>
    /// 撤销 Token
    /// </summary>
    public async Task<Result> RevokeTokenAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{RevokeUrl}/{accessToken}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var httpClient = _httpClientFactory.CreateClient("EpicAuth");
            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Token 撤销失败 | StatusCode={Code}", response.StatusCode);
            }
            else
            {
                _logger.Information("Token 已撤销");
            }

            // 即使撤销失败，登出流程仍应继续
            return Result.Ok();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warning(ex, "Token 撤销异常（将继续登出流程）");
            return Result.Ok();
        }
    }

    /// <summary>
    /// 用授权码换取 Token
    /// </summary>
    private async Task<Result<TokenPair>> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = content,
            };
            AddClientAuth(request);

            using var httpClient = _httpClientFactory.CreateClient("EpicAuth");
            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Token 交换失败 | StatusCode={Code}", response.StatusCode);
                return Result.Fail<TokenPair>(new Error
                {
                    Code = "AUTH_TOKEN_EXCHANGE_FAILED",
                    UserMessage = "登录授权失败，请重试",
                    TechnicalMessage = $"HTTP {(int)response.StatusCode}: {body}",
                    CanRetry = true,
                    Severity = ErrorSeverity.Error,
                });
            }

            var tokenPair = ParseTokenResponse(body);
            _logger.Information("Token 交换成功 | ExpiresAt={ExpiresAt}", tokenPair.ExpiresAt);
            return Result.Ok(tokenPair);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "Token 交换异常");
            return Result.Fail<TokenPair>(new Error
            {
                Code = "AUTH_TOKEN_EXCHANGE_EXCEPTION",
                UserMessage = "登录过程中出错，请重试",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }
    }

    private static TokenPair ParseTokenResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty;
        var expiresIn = root.GetProperty("expires_in").GetInt32();
        var accountId = root.TryGetProperty("account_id", out var aid) ? aid.GetString() ?? string.Empty : string.Empty;
        var displayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? string.Empty : string.Empty;

        return new TokenPair
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            AccountId = accountId,
            DisplayName = displayName,
        };
    }

    private static (HttpListener listener, string redirectUri) StartListener()
    {
        // 尝试使用固定端口，如果不可用则随机分配
        var ports = new[] { 6780, 6781, 6782, 6783, 6784 };
        foreach (var port in ports)
        {
            try
            {
                var listener = new HttpListener();
                var prefix = $"{LoopbackHost}:{port}/";
                listener.Prefixes.Add(prefix);
                listener.Start();
                return (listener, $"{LoopbackHost}:{port}{RedirectPath}");
            }
            catch (HttpListenerException)
            {
                // 端口被占用，尝试下一个
            }
        }

        throw new InvalidOperationException("无法找到可用端口启动 OAuth 回调监听器 (tried ports 6780-6784)");
    }

    private static async Task<string?> WaitForCallbackAsync(HttpListener listener, CancellationToken ct)
    {
        // 设置超时（3 分钟）
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(3));

        try
        {
            var context = await listener.GetContextAsync().WaitAsync(cts.Token);
            var request = context.Request;
            var code = request.QueryString["code"];

            // 返回成功页面
            var responseHtml = code is not null
                ? "<html><body><h1>登录成功！</h1><p>请返回 HelsincyEpicLauncher。此页面可以关闭。</p></body></html>"
                : "<html><body><h1>登录失败</h1><p>未收到授权码。请重试。</p></body></html>";

            var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, cts.Token);
            context.Response.Close();

            return code;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }

    private void AddClientAuth(HttpRequestMessage request)
    {
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    }
}
