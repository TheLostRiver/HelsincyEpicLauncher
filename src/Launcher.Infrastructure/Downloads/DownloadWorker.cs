// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 执行单个下载任务。调度器负责并发和取消，Worker 负责读取任务、下载数据并回写状态。
/// </summary>
internal sealed class DownloadWorker : IDownloadTaskExecutor
{
    private readonly IDownloadTaskRepository _repository;
    private readonly IDownloadScheduler _scheduler;
    private readonly IDownloadRuntimeStore _runtimeStore;
    private readonly IChunkDownloader _chunkDownloader;
    private readonly ILogger _logger = Log.ForContext<DownloadWorker>();

    public DownloadWorker(
        IDownloadTaskRepository repository,
        IDownloadScheduler scheduler,
        IDownloadRuntimeStore runtimeStore,
        IChunkDownloader chunkDownloader)
    {
        _repository = repository;
        _scheduler = scheduler;
        _runtimeStore = runtimeStore;
        _chunkDownloader = chunkDownloader;
    }

    public async Task ExecuteAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            _logger.Warning("下载任务不存在，释放调度位 | TaskId={TaskId}", taskId);
            _scheduler.NotifyCompleted(taskId);
            return;
        }

        try
        {
            await ExecuteCoreAsync(task, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await MarkPausedAsync(task).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(task, ex.Message, canRetry: true, CancellationToken.None).ConfigureAwait(false);
            _logger.Error(ex, "下载任务执行异常 | TaskId={TaskId}", taskId);
        }
        finally
        {
            _scheduler.NotifyCompleted(taskId);
        }
    }

    private async Task ExecuteCoreAsync(DownloadTask task, CancellationToken ct)
    {
        await TransitionAsync(task, DownloadState.Preparing, ct).ConfigureAwait(false);
        await TransitionAsync(task, DownloadState.FetchingManifest, ct).ConfigureAwait(false);
        await TransitionAsync(task, DownloadState.AllocatingDisk, ct).ConfigureAwait(false);
        await TransitionAsync(task, DownloadState.DownloadingChunks, ct).ConfigureAwait(false);

        var result = await _chunkDownloader.DownloadChunkAsync(
            new ChunkDownloadRequest
            {
                Url = task.DownloadUrl,
                DestinationPath = task.InstallPath,
                RangeStart = 0,
                RangeEnd = task.TotalBytes > 0 ? task.TotalBytes - 1 : 0,
            },
            new Progress<long>(downloadedBytes =>
            {
                task.UpdateProgress(downloadedBytes, speedBytesPerSecond: 0);
                _runtimeStore.UpdateProgress(task.Id, task.UiState, task.DownloadedBytes, task.TotalBytes);
            }),
            ct).ConfigureAwait(false);

        if (result.IsFailure)
        {
            var error = result.Error!;
            var message = error.TechnicalMessage ?? error.UserMessage;
            await MarkFailedAsync(task, message, error.CanRetry, ct).ConfigureAwait(false);
            return;
        }

        task.UpdateProgress(result.Value!.BytesDownloaded, speedBytesPerSecond: 0);
        _runtimeStore.UpdateProgress(task.Id, task.UiState, task.DownloadedBytes, task.TotalBytes);

        await TransitionAsync(task, DownloadState.VerifyingDownload, ct).ConfigureAwait(false);
        await TransitionAsync(task, DownloadState.Finalizing, ct).ConfigureAwait(false);
        await TransitionAsync(task, DownloadState.Completed, ct).ConfigureAwait(false);
        _runtimeStore.NotifyCompleted(task.Id, task.AssetId, task.InstallPath);
    }

    private async Task TransitionAsync(DownloadTask task, DownloadState target, CancellationToken ct)
    {
        if (task.State == target)
            return;

        var result = task.TransitionTo(target);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.TechnicalMessage ?? result.Error?.UserMessage);

        await _repository.UpdateAsync(task, ct).ConfigureAwait(false);
    }

    private async Task MarkFailedAsync(DownloadTask task, string errorMessage, bool canRetry, CancellationToken ct)
    {
        task.SetError(errorMessage);
        if (task.CanTransitionTo(DownloadState.Failed))
        {
            task.TransitionTo(DownloadState.Failed);
        }

        await _repository.UpdateAsync(task, ct).ConfigureAwait(false);
        _runtimeStore.NotifyFailed(task.Id, task.AssetId, errorMessage, canRetry);
    }

    private async Task MarkPausedAsync(DownloadTask task)
    {
        if (task.CanTransitionTo(DownloadState.PausingChunks))
        {
            task.TransitionTo(DownloadState.PausingChunks);
        }

        if (task.CanTransitionTo(DownloadState.Paused))
        {
            task.TransitionTo(DownloadState.Paused);
        }

        await _repository.UpdateAsync(task, CancellationToken.None).ConfigureAwait(false);
        _runtimeStore.RemoveSnapshot(task.Id);
    }
}
