// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Auth.Contracts;

/// <summary>
/// 认证用户信息
/// </summary>
public sealed class AuthUserInfo
{
    /// <summary>Epic Games 账号 ID</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>显示名</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>邮箱</summary>
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Token 对（Access Token + Refresh Token）
/// </summary>
public sealed class TokenPair
{
    /// <summary>访问令牌</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>刷新令牌</summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>访问令牌过期时间 (UTC)</summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>用户账户 ID</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>显示名</summary>
    public string DisplayName { get; init; } = string.Empty;
}

/// <summary>
/// 会话过期事件
/// </summary>
public sealed record SessionExpiredEvent(string Reason);
