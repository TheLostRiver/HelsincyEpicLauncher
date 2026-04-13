// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Auth.Contracts;

/// <summary>
/// Token 安全存储接口。由 Infrastructure 层实现（Windows Credential Locker）。
/// </summary>
public interface ITokenStore
{
    /// <summary>保存 Token 对</summary>
    Task SaveTokensAsync(TokenPair tokens, CancellationToken ct = default);

    /// <summary>加载缓存的 Token 对</summary>
    Task<TokenPair?> LoadTokensAsync(CancellationToken ct = default);

    /// <summary>清除所有存储的 Token</summary>
    Task ClearAsync(CancellationToken ct = default);
}
