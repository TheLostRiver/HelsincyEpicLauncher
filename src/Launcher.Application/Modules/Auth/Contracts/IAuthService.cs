// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared;

namespace Launcher.Application.Modules.Auth.Contracts;

/// <summary>
/// Epic Games 认证服务。处理 OAuth 2.0 登录流程和 Token 管理。
/// </summary>
public interface IAuthService
{
    /// <summary>当前是否已认证</summary>
    bool IsAuthenticated { get; }

    /// <summary>当前登录用户信息</summary>
    AuthUserInfo? CurrentUser { get; }

    /// <summary>启动 OAuth 登录流程</summary>
    Task<Result<AuthUserInfo>> LoginAsync(CancellationToken ct = default);

    /// <summary>登出</summary>
    Task<Result> LogoutAsync(CancellationToken ct = default);

    /// <summary>获取有效的 Access Token（自动刷新过期 Token）</summary>
    Task<Result<string>> GetAccessTokenAsync(CancellationToken ct = default);

    /// <summary>尝试从缓存恢复会话（启动时调用）</summary>
    Task<Result<AuthUserInfo>> TryRestoreSessionAsync(CancellationToken ct = default);

    /// <summary>会话过期事件</summary>
    event Action<SessionExpiredEvent>? SessionExpired;
}
