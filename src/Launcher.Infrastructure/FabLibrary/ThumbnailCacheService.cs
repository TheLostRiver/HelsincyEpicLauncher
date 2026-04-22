// Copyright (c) Helsincy. All rights reserved.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Serilog;

namespace Launcher.Infrastructure.FabLibrary;

/// <summary>
/// 缩略图 LRU 磁盘缓存服务。
/// 使用 URL SHA-256 哈希作为文件名，按访问时间 LRU 淘汰。
/// </summary>
public sealed class ThumbnailCacheService : IThumbnailCacheService
{
    private static readonly ILogger Logger = Log.ForContext<ThumbnailCacheService>();

    private readonly HttpClient _httpClient;
    private readonly string _cacheDir;
    private readonly int _maxCacheItems;
    private readonly TimeSpan _maxAge;

    /// <summary>并发下载锁（防止同一 URL 被重复下载）</summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _downloadLocks = new();

    /// <summary>内存索引：URL hash → 最后访问时间</summary>
    private readonly ConcurrentDictionary<string, DateTime> _accessIndex = new();

    public ThumbnailCacheService(HttpClient httpClient, string? cacheDir = null, int maxCacheItems = 2000, TimeSpan? maxAge = null)
    {
        _httpClient = httpClient;
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Helsincy", "EpicLauncher", "ThumbnailCache");
        _maxCacheItems = maxCacheItems;
        _maxAge = maxAge ?? TimeSpan.FromDays(7);

        Directory.CreateDirectory(_cacheDir);
        BuildAccessIndex();
    }

    public async Task<string?> GetOrDownloadAsync(string imageUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var hash = ComputeUrlHash(imageUrl);
        var cacheFileName = BuildCacheFileName(hash, imageUrl);
        var cachedPath = Path.Combine(_cacheDir, cacheFileName);

        // 命中缓存
        if (File.Exists(cachedPath))
        {
            _accessIndex[cacheFileName] = DateTime.UtcNow;
            return cachedPath;
        }

        // 获取或创建该 URL 的下载锁
        var downloadLock = _downloadLocks.GetOrAdd(hash, _ => new SemaphoreSlim(1, 1));

        await downloadLock.WaitAsync(ct);
        try
        {
            // 双重检查：等待锁期间可能已被其他线程下载
            if (File.Exists(cachedPath))
            {
                _accessIndex[cacheFileName] = DateTime.UtcNow;
                return cachedPath;
            }

            // 下载
            Logger.Debug("开始下载缩略图 | Url={Url} | CachePath={Path}", imageUrl, cachedPath);
            using var response = await _httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("缩略图下载失败 {Url}: {Status}", imageUrl, response.StatusCode);
                return null;
            }

            // 写入临时文件后原子移动
            var tempPath = cachedPath + ".tmp";
            await using (var stream = await response.Content.ReadAsStreamAsync(ct))
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream, ct);
            }

            File.Move(tempPath, cachedPath, overwrite: true);
            _accessIndex[cacheFileName] = DateTime.UtcNow;
            Logger.Debug("缩略图下载完成 | Url={Url} | CachePath={Path}", imageUrl, cachedPath);

            return cachedPath;
        }
        catch (OperationCanceledException ex)
        {
            Logger.Warning(ex, "缩略图下载已取消 {Url}", imageUrl);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "缩略图缓存失败 {Url}", imageUrl);
            return null;
        }
        finally
        {
            downloadLock.Release();
        }
    }

    public Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;

            // 删除过期文件
            foreach (var (hash, accessTime) in _accessIndex)
            {
                if (ct.IsCancellationRequested) break;
                if (now - accessTime > _maxAge)
                {
                    var path = Path.Combine(_cacheDir, hash);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        _accessIndex.TryRemove(hash, out _);
                    }
                }
            }

            // LRU 淘汰超出上限的条目
            if (_accessIndex.Count > _maxCacheItems)
            {
                var toRemove = _accessIndex
                    .OrderBy(kv => kv.Value)
                    .Take(_accessIndex.Count - _maxCacheItems)
                    .ToList();

                foreach (var (hash, _) in toRemove)
                {
                    if (ct.IsCancellationRequested) break;
                    var path = Path.Combine(_cacheDir, hash);
                    if (File.Exists(path))
                        File.Delete(path);
                    _accessIndex.TryRemove(hash, out _);
                }
            }

            Logger.Debug("缩略图缓存清理完成，剩余 {Count} 个", _accessIndex.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "缩略图缓存清理异常");
        }

        return Task.CompletedTask;
    }

    /// <summary>启动时从磁盘构建访问索引</summary>
    private void BuildAccessIndex()
    {
        if (!Directory.Exists(_cacheDir)) return;

        foreach (var file in Directory.GetFiles(_cacheDir))
        {
            if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            var fileName = Path.GetFileName(file);
            var lastAccess = File.GetLastWriteTimeUtc(file);
            _accessIndex[fileName] = lastAccess;
        }

        Logger.Debug("缩略图缓存索引已构建，{Count} 个条目", _accessIndex.Count);
    }

    private static string ComputeUrlHash(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexStringLower(bytes);
    }

    private static string BuildCacheFileName(string hash, string imageUrl)
    {
        var extension = TryGetImageExtension(imageUrl);
        return string.IsNullOrWhiteSpace(extension)
            ? hash
            : hash + extension;
    }

    private static string TryGetImageExtension(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            return string.Empty;
        }

        return extension.ToLowerInvariant();
    }
}
