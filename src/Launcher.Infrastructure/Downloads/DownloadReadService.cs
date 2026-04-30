// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 下载只读查询服务。基于 Repository + Scheduler + RuntimeStore 聚合查询。
/// </summary>
public sealed class DownloadReadService : IDownloadReadService
{
    private readonly IDownloadTaskRepository _repository;
    private readonly IDownloadScheduler _scheduler;
    private readonly DownloadRuntimeStore _runtimeStore;
    private readonly ILogger _logger = Log.ForContext<DownloadReadService>();

    public DownloadReadService(
        IDownloadTaskRepository repository,
        IDownloadScheduler scheduler,
        DownloadRuntimeStore runtimeStore)
    {
        _repository = repository;
        _scheduler = scheduler;
        _runtimeStore = runtimeStore;
    }

    public int ActiveCount => _scheduler.ActiveCount;

    public async Task<DownloadStatusSummary?> GetStatusAsync(string assetId, CancellationToken ct)
    {
        var task = await _repository.GetByAssetIdAsync(assetId, ct);
        return task is null ? null : MapToSummary(task);
    }

    public async Task<IReadOnlyList<DownloadStatusSummary>> GetActiveDownloadsAsync(CancellationToken ct)
    {
        var tasks = await _repository.GetActiveTasksAsync(ct);
        return tasks.Select(MapToSummary).ToList();
    }

    public async Task<IReadOnlyList<DownloadStatusSummary>> GetHistoryAsync(int limit, CancellationToken ct)
    {
        var tasks = await _repository.GetHistoryAsync(limit, ct);
        return tasks.Select(MapToSummary).ToList();
    }

    private DownloadStatusSummary MapToSummary(DownloadTask task)
    {
        var uiState = task.UiState;
        var snapshot = _runtimeStore.GetSnapshot(task.Id);

        // 如果有实时快照，优先使用快照中的速度/进度
        var bytesPerSecond = snapshot?.SpeedBytesPerSecond ?? task.SpeedBytesPerSecond;
        var downloadedBytes = snapshot?.DownloadedBytes ?? task.DownloadedBytes;
        var totalBytes = snapshot?.TotalBytes ?? task.TotalBytes;

        return new DownloadStatusSummary
        {
            TaskId = task.Id,
            AssetId = task.AssetId,
            AssetName = task.DisplayName,
            Status = MapStatus(uiState),
            UiState = uiState,
            Progress = totalBytes > 0 ? (double)downloadedBytes / totalBytes : 0,
            DownloadedBytes = downloadedBytes,
            TotalBytes = totalBytes,
            BytesPerSecond = bytesPerSecond,
            EstimatedRemaining = snapshot?.EstimatedRemaining
                ?? (bytesPerSecond > 0
                    ? TimeSpan.FromSeconds((double)(totalBytes - downloadedBytes) / bytesPerSecond)
                    : null),
            CanPause = task.CanTransitionTo(DownloadState.PausingChunks) || task.State == DownloadState.Queued,
            CanResume = task.CanTransitionTo(DownloadState.Queued) && task.State is DownloadState.Paused or DownloadState.Failed,
            CanCancel = task.CanTransitionTo(DownloadState.Cancelled),
            ErrorMessage = task.LastError,
        };
    }

    private static DownloadStatusKind MapStatus(DownloadUiState uiState) => uiState switch
    {
        DownloadUiState.Queued => DownloadStatusKind.Queued,
        DownloadUiState.Downloading => DownloadStatusKind.Downloading,
        DownloadUiState.Paused => DownloadStatusKind.Paused,
        DownloadUiState.Verifying => DownloadStatusKind.Verifying,
        DownloadUiState.Completed => DownloadStatusKind.Completed,
        DownloadUiState.Failed => DownloadStatusKind.Failed,
        DownloadUiState.Cancelled => DownloadStatusKind.Cancelled,
        _ => DownloadStatusKind.Failed,
    };
}
