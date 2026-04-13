// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Diagnostics.Contracts;
using Launcher.Shared;
using Launcher.Shared.Configuration;
using Serilog;

namespace Launcher.Infrastructure.Diagnostics;

/// <summary>
/// 缓存管理实现。扫描缩略图/Manifest/日志目录，支持分类清理。
/// </summary>
internal sealed class CacheManager : ICacheManager
{
    private readonly ILogger _logger = Log.ForContext<CacheManager>();
    private readonly IAppConfigProvider _configProvider;

    // 缓存子目录约定
    private const string ThumbnailSubDir = "Thumbnails";
    private const string ManifestSubDir = "Manifests";

    public CacheManager(IAppConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    public Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        var thumbDir = Path.Combine(_configProvider.CachePath, ThumbnailSubDir);
        var manifestDir = Path.Combine(_configProvider.CachePath, ManifestSubDir);
        var logDir = _configProvider.LogPath;

        var (thumbBytes, thumbCount) = GetDirectoryStats(thumbDir);
        var (manifestBytes, manifestCount) = GetDirectoryStats(manifestDir);
        var (logBytes, logCount) = GetDirectoryStats(logDir);

        var stats = new CacheStatistics
        {
            ThumbnailCacheBytes = thumbBytes,
            ThumbnailFileCount = thumbCount,
            ManifestCacheBytes = manifestBytes,
            ManifestFileCount = manifestCount,
            LogFileBytes = logBytes,
            LogFileCount = logCount,
        };

        _logger.Debug("缓存统计已采集 | 缩略图={ThumbMB:F1}MB | Manifest={ManifestMB:F1}MB | 日志={LogMB:F1}MB",
            thumbBytes / 1048576.0, manifestBytes / 1048576.0, logBytes / 1048576.0);

        return Task.FromResult(stats);
    }

    public Task<Result> ClearThumbnailCacheAsync()
    {
        var dir = Path.Combine(_configProvider.CachePath, ThumbnailSubDir);
        return ClearDirectoryAsync(dir, "缩略图缓存");
    }

    public Task<Result> ClearManifestCacheAsync()
    {
        var dir = Path.Combine(_configProvider.CachePath, ManifestSubDir);
        return ClearDirectoryAsync(dir, "Manifest 缓存");
    }

    public Task<Result> ClearLogCacheAsync()
    {
        return ClearLogFilesAsync();
    }

    public async Task<Result> ClearAllCacheAsync()
    {
        var r1 = await ClearThumbnailCacheAsync();
        var r2 = await ClearManifestCacheAsync();
        var r3 = await ClearLogCacheAsync();

        if (!r1.IsSuccess || !r2.IsSuccess || !r3.IsSuccess)
        {
            return Result.Fail(new Error
            {
                Code = "CACHE_CLEAR_PARTIAL",
                UserMessage = "部分缓存清理失败，请稍后重试",
                TechnicalMessage = $"Thumb={r1.IsSuccess}, Manifest={r2.IsSuccess}, Log={r3.IsSuccess}",
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            });
        }

        _logger.Information("全部缓存已清理");
        return Result.Ok();
    }

    /// <summary>
    /// 清理日志文件，保留最近 1 天内的文件
    /// </summary>
    private Task<Result> ClearLogFilesAsync()
    {
        var logDir = _configProvider.LogPath;
        if (!Directory.Exists(logDir))
            return Task.FromResult(Result.Ok());

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-1);
            var deletedCount = 0;
            long deletedBytes = 0;

            foreach (var file in Directory.GetFiles(logDir, "*.log"))
            {
                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (lastWrite < cutoff)
                {
                    var size = new FileInfo(file).Length;
                    File.Delete(file);
                    deletedCount++;
                    deletedBytes += size;
                }
            }

            _logger.Information("日志缓存已清理 | 删除={Count}个文件 | 释放={MB:F1}MB",
                deletedCount, deletedBytes / 1048576.0);
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "清理日志缓存失败");
            return Task.FromResult(Result.Fail(new Error
            {
                Code = "CACHE_CLEAR_LOG_FAILED",
                UserMessage = "日志缓存清理失败",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            }));
        }
    }

    private Task<Result> ClearDirectoryAsync(string dirPath, string label)
    {
        if (!Directory.Exists(dirPath))
            return Task.FromResult(Result.Ok());

        try
        {
            var deletedCount = 0;
            long deletedBytes = 0;

            foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
            {
                var size = new FileInfo(file).Length;
                File.Delete(file);
                deletedCount++;
                deletedBytes += size;
            }

            _logger.Information("{Label}已清理 | 删除={Count}个文件 | 释放={MB:F1}MB",
                label, deletedCount, deletedBytes / 1048576.0);
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "清理{Label}失败", label);
            return Task.FromResult(Result.Fail(new Error
            {
                Code = "CACHE_CLEAR_FAILED",
                UserMessage = $"{label}清理失败",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            }));
        }
    }

    private static (long totalBytes, int fileCount) GetDirectoryStats(string dirPath)
    {
        if (!Directory.Exists(dirPath))
            return (0, 0);

        try
        {
            var files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
            long totalBytes = 0;
            foreach (var file in files)
            {
                totalBytes += new FileInfo(file).Length;
            }

            return (totalBytes, files.Length);
        }
        catch
        {
            return (0, 0);
        }
    }
}
