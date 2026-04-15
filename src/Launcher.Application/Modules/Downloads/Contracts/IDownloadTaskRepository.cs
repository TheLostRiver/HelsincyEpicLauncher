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
    Task<IReadOnlyList<DownloadTaskId>> GetTaskIdsByStateAsync(DownloadState state, CancellationToken ct = default);
    Task<IReadOnlyList<DownloadTaskId>> GetTaskIdsExcludingStatesAsync(IReadOnlyList<DownloadState> excludedStates, CancellationToken ct = default);
    Task<IReadOnlyList<DownloadTask>> GetHistoryAsync(int limit, CancellationToken ct = default);
    Task InsertAsync(DownloadTask task, CancellationToken ct = default);
    Task UpdateAsync(DownloadTask task, CancellationToken ct = default);

    // Checkpoint
    Task SaveCheckpointAsync(DownloadCheckpoint checkpoint, CancellationToken ct = default);
    Task<DownloadCheckpoint?> GetCheckpointAsync(DownloadTaskId taskId, CancellationToken ct = default);
    Task DeleteCheckpointAsync(DownloadTaskId taskId, CancellationToken ct = default);
}
