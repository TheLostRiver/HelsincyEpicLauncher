// Copyright (c) Helsincy. All rights reserved.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Shared.Configuration;
using Serilog;

namespace Launcher.Infrastructure.Auth;

/// <summary>
/// Token 存储实现。使用 DPAPI 加密文件存储 Token。
/// Task 3.2 将升级为 Windows Credential Locker。
/// </summary>
internal sealed class FileTokenStore : ITokenStore, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<FileTokenStore>();
    private readonly string _tokenFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public FileTokenStore(IAppConfigProvider configProvider)
    {
        _tokenFilePath = Path.Combine(configProvider.DataPath, ".tokens");
    }

    public async Task SaveTokensAsync(TokenPair tokens, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(new TokenData
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt = tokens.ExpiresAt,
                AccountId = tokens.AccountId,
                DisplayName = tokens.DisplayName,
            });

            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

            await File.WriteAllBytesAsync(_tokenFilePath, encryptedBytes, ct);
            _logger.Debug("Token 已保存到文件");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "保存 Token 失败");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<TokenPair?> LoadTokensAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            if (!File.Exists(_tokenFilePath))
                return null;

            var encryptedBytes = await File.ReadAllBytesAsync(_tokenFilePath, ct);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);

            var data = JsonSerializer.Deserialize<TokenData>(json);
            if (data is null)
                return null;

            _logger.Debug("Token 已从文件加载");
            return new TokenPair
            {
                AccessToken = data.AccessToken,
                RefreshToken = data.RefreshToken,
                ExpiresAt = data.ExpiresAt.Kind == DateTimeKind.Utc
                    ? data.ExpiresAt
                    : DateTime.SpecifyKind(data.ExpiresAt, DateTimeKind.Utc),
                AccountId = data.AccountId,
                DisplayName = data.DisplayName,
            };
        }
        catch (CryptographicException ex)
        {
            _logger.Warning(ex, "Token 文件解密失败（可能是不同用户加密的），将清除旧数据");
            await ClearInternalAsync();
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "加载 Token 失败");
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await ClearInternalAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private Task ClearInternalAsync()
    {
        if (File.Exists(_tokenFilePath))
        {
            File.Delete(_tokenFilePath);
            _logger.Debug("Token 文件已删除");
        }

        return Task.CompletedTask;
    }

    private sealed class TokenData
    {
        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
        public string AccountId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    public void Dispose() => _semaphore.Dispose();
}
