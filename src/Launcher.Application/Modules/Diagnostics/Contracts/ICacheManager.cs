// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared;

namespace Launcher.Application.Modules.Diagnostics.Contracts;

/// <summary>
/// 缓存管理接口。获取缓存统计、分类清理或全部清理。
/// </summary>
public interface ICacheManager
{
    /// <summary>获取缓存使用统计</summary>
    Task<CacheStatistics> GetCacheStatisticsAsync();

    /// <summary>清理缩略图缓存</summary>
    Task<Result> ClearThumbnailCacheAsync();

    /// <summary>清理 Manifest/搜索缓存</summary>
    Task<Result> ClearManifestCacheAsync();

    /// <summary>清理日志文件缓存（保留最近 1 天）</summary>
    Task<Result> ClearLogCacheAsync();

    /// <summary>清理全部缓存</summary>
    Task<Result> ClearAllCacheAsync();
}
