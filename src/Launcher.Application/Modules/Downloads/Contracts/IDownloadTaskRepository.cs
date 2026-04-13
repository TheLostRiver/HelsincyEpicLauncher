// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Downloads;

namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载任务持久化仓储接口
/// </summary>
public interface IDownloadTaskRepository
{
    Task<DownloadTask?> GetByIdAsync(DownloadTaskId id, CancellationToken ct = default);
    Task<DownloadTask?> GetByAssetIdAsync(string assetId, CancellationToken ct = default);
    Task<IReadOnlyList<DownloadTask>> GetActiveTasksAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DownloadTask>> GetHistoryAsync(int limit, CancellationToken ct = default);
    Task InsertAsync(DownloadTask task, CancellationToken ct = default);
    Task UpdateAsync(DownloadTask task, CancellationToken ct = default);
}
