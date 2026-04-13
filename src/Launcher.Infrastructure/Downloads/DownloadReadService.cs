// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 下载只读查询服务。基于 Repository + Scheduler 聚合查询。
/// </summary>
public sealed class DownloadReadService : IDownloadReadService
{
    private readonly IDownloadTaskRepository _repository;
    private readonly DownloadScheduler _scheduler;
    private readonly ILogger _logger = Log.ForContext<DownloadReadService>();

    public DownloadReadService(
        IDownloadTaskRepository repository,
        DownloadScheduler scheduler)
    {
        _repository = repository;
        _scheduler = scheduler;
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

    private static DownloadStatusSummary MapToSummary(DownloadTask task)
    {
        var uiState = task.UiState;
        return new DownloadStatusSummary
        {
            TaskId = task.Id,
            AssetId = task.AssetId,
            AssetName = task.DisplayName,
            UiState = uiState,
            Progress = task.TotalBytes > 0 ? (double)task.DownloadedBytes / task.TotalBytes : 0,
            DownloadedBytes = task.DownloadedBytes,
            TotalBytes = task.TotalBytes,
            BytesPerSecond = task.SpeedBytesPerSecond,
            EstimatedRemaining = task.SpeedBytesPerSecond > 0
                ? TimeSpan.FromSeconds((double)(task.TotalBytes - task.DownloadedBytes) / task.SpeedBytesPerSecond)
                : null,
            CanPause = task.CanTransitionTo(DownloadState.PausingChunks) || task.State == DownloadState.Queued,
            CanResume = task.CanTransitionTo(DownloadState.Queued) && task.State is DownloadState.Paused or DownloadState.Failed,
            CanCancel = task.CanTransitionTo(DownloadState.Cancelled),
            ErrorMessage = task.LastError,
        };
    }
}
