// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.FabLibrary.Contracts;

/// <summary>
/// 缩略图缓存服务。支持 URL → 本地文件路径的 LRU 磁盘缓存。
/// </summary>
public interface IThumbnailCacheService
{
    /// <summary>
    /// 获取缩略图本地缓存路径。命中缓存直接返回，否则下载并缓存。
    /// </summary>
    /// <param name="imageUrl">远程图片 URL</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>本地缓存文件路径，失败返回 null</returns>
    Task<string?> GetOrDownloadAsync(string imageUrl, CancellationToken ct);

    /// <summary>清除过期缓存</summary>
    Task CleanupAsync(CancellationToken ct);
}
