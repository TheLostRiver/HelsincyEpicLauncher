// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 下载任务编排器。管理下载任务的完整生命周期。
/// </summary>
internal sealed class DownloadOrchestrator : IDownloadOrchestrator
{
    private readonly IDownloadTaskRepository _repository;
    private readonly IDownloadScheduler _scheduler;
    private readonly IDownloadTaskExecutor _executor;
    private readonly ILogger _logger = Log.ForContext<DownloadOrchestrator>();

    public DownloadOrchestrator(
        IDownloadTaskRepository repository,
        IDownloadScheduler scheduler,
        IDownloadTaskExecutor executor)
    {
        _repository = repository;
        _scheduler = scheduler;
        _executor = executor;
        _scheduler.TaskReady += _executor.ExecuteAsync;
    }

    /// <summary>
    /// 创建并入队下载任务
    /// </summary>
    public async Task<Result<DownloadTaskId>> EnqueueAsync(StartDownloadRequest request, CancellationToken ct)
    {
        // 验证磁盘空间
        if (request.TotalBytes > 0)
        {
            var pathRoot = Path.GetPathRoot(request.DestinationPath);
            if (string.IsNullOrEmpty(pathRoot))
            {
                return Result.Fail<DownloadTaskId>(DownloadErrors.InvalidPath(request.DestinationPath));
            }

            var driveInfo = new DriveInfo(pathRoot);
            var requiredSpace = (long)(request.TotalBytes * AppConstants.Download.DiskSpaceBufferFactor);
            if (driveInfo.AvailableFreeSpace < requiredSpace)
            {
                _logger.Warning("磁盘空间不足: 需要 {Required} 字节, 可用 {Available} 字节",
                    requiredSpace, driveInfo.AvailableFreeSpace);
                return Result.Fail<DownloadTaskId>(DownloadErrors.InsufficientDiskSpace(requiredSpace, driveInfo.AvailableFreeSpace));
            }
        }

        // 检查是否已有相同 AssetId 的活跃任务
        var existing = await _repository.GetByAssetIdAsync(request.AssetId, ct);
        if (existing is not null && existing.State is not (DownloadState.Completed or DownloadState.Cancelled or DownloadState.Failed))
        {
            _logger.Warning("资源 {AssetId} 已有活跃下载任务 {TaskId}", request.AssetId, existing.Id);
            return Result.Fail<DownloadTaskId>(DownloadErrors.Duplicate(request.AssetId));
        }

        // 创建领域实体
        var taskId = DownloadTaskId.New();
        var downloadTask = new DownloadTask(
            taskId,
            request.AssetId,
            request.AssetName,
            request.DownloadUrl,
            request.DestinationPath,
            request.TotalBytes,
            request.Priority);

        // 持久化
        await _repository.InsertAsync(downloadTask, ct);
        _logger.Information("下载任务已创建: {TaskId}, 资源: {AssetId}, 名称: {Name}",
            taskId, request.AssetId, request.AssetName);

        // 推入调度器队列
        await _scheduler.QueueAsync(taskId, request.Priority, ct);

        return Result.Ok(taskId);
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    public async Task<Result> PauseAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task is null)
            return Result.Fail(DownloadErrors.NotFound(taskId));

        // 正在下载中 -> 暂停
        if (task.CanTransitionTo(DownloadState.PausingChunks))
        {
            var result = task.TransitionTo(DownloadState.PausingChunks);
            if (result.IsFailure) return result;

            _scheduler.RequestPause(taskId);
            await _repository.UpdateAsync(task, ct);
            _logger.Information("下载任务暂停中: {TaskId}", taskId);
            return Result.Ok();
        }

        // 排队中的任务直接移除调度
        if (task.State == DownloadState.Queued)
        {
            _scheduler.Dequeue(taskId);
            _logger.Information("下载任务从队列移除: {TaskId}", taskId);
            return Result.Ok();
        }

        return Result.Fail(DownloadErrors.CannotPause(taskId, task.State));
    }

    /// <summary>
    /// 恢复任务
    /// </summary>
    public async Task<Result> ResumeAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task is null)
            return Result.Fail(DownloadErrors.NotFound(taskId));

        if (!task.CanTransitionTo(DownloadState.Queued))
            return Result.Fail(DownloadErrors.CannotResume(taskId, task.State));

        var result = task.TransitionTo(DownloadState.Queued);
        if (result.IsFailure) return result;

        task.ClearError();
        await _repository.UpdateAsync(task, ct);

        await _scheduler.QueueAsync(taskId, task.Priority, ct);
        _logger.Information("下载任务恢复: {TaskId}", taskId);

        return Result.Ok();
    }

    /// <summary>
    /// 取消任务
    /// </summary>
    public async Task<Result> CancelAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task is null)
            return Result.Fail(DownloadErrors.NotFound(taskId));

        if (!task.CanTransitionTo(DownloadState.Cancelled))
            return Result.Fail(DownloadErrors.CannotCancel(taskId, task.State));

        var result = task.TransitionTo(DownloadState.Cancelled);
        if (result.IsFailure) return result;

        _scheduler.Dequeue(taskId);
        await _repository.UpdateAsync(task, ct);
        _logger.Information("下载任务已取消: {TaskId}", taskId);

        return Result.Ok();
    }

    /// <summary>
    /// 崩溃恢复：加载所有未完成的任务并重新调度
    /// </summary>
    public async Task RecoverAsync(CancellationToken ct)
    {
        var activeTasks = await _repository.GetActiveTasksAsync(ct);
        _logger.Information("崩溃恢复: 发现 {Count} 个未完成任务", activeTasks.Count);

        foreach (var task in activeTasks)
        {
            await RecoverSingleTaskAsync(task, ct);
        }
    }

    private async Task RecoverSingleTaskAsync(DownloadTask task, CancellationToken ct)
    {
        switch (task.State)
        {
            case DownloadState.Paused:
                // 暂停任务保持原状，不自动恢复
                break;

            case DownloadState.Queued:
                // 已在队列中，直接重新调度
                await _scheduler.QueueAsync(task.Id, task.Priority, ct);
                _logger.Information("恢复任务 {TaskId} 重新入队（原 Queued）", task.Id);
                break;

            default:
                // 中间状态（Preparing/Downloading/Verifying 等）→ 先转 Failed 再转 Queued
                await RecoverInterruptedTaskAsync(task, ct);
                break;
        }
    }

    private async Task RecoverInterruptedTaskAsync(DownloadTask task, CancellationToken ct)
    {
        if (task.CanTransitionTo(DownloadState.Failed))
            task.TransitionTo(DownloadState.Failed);

        if (task.CanTransitionTo(DownloadState.Queued))
        {
            task.TransitionTo(DownloadState.Queued);
            task.ClearError();
            await _repository.UpdateAsync(task, ct);
            await _scheduler.QueueAsync(task.Id, task.Priority, ct);
            _logger.Information("恢复任务 {TaskId} 重新入队（经 Failed 中转）", task.Id);
        }
        else
        {
            await _repository.UpdateAsync(task, ct);
            _logger.Warning("任务 {TaskId} 无法恢复，标记为 Failed | 当前状态={State}", task.Id, task.State);
        }
    }

    /// <summary>
    /// 获取所有活跃（非 Paused）任务的 ID
    /// </summary>
    public async Task<IReadOnlyList<DownloadTaskId>> GetActiveTaskIdsAsync(CancellationToken ct)
    {
        return await _repository.GetTaskIdsExcludingStatesAsync(
            [DownloadState.Paused, DownloadState.Completed, DownloadState.Cancelled, DownloadState.Failed], ct);
    }

    /// <summary>
    /// 获取所有已暂停任务的 ID
    /// </summary>
    public async Task<IReadOnlyList<DownloadTaskId>> GetPausedTaskIdsAsync(CancellationToken ct)
    {
        return await _repository.GetTaskIdsByStateAsync(DownloadState.Paused, ct);
    }

    /// <summary>
    /// 调整任务优先级
    /// </summary>
    public async Task<Result> SetPriorityAsync(DownloadTaskId taskId, int priority, CancellationToken ct)
    {
        var task = await _repository.GetByIdAsync(taskId, ct);
        if (task is null)
            return Result.Fail(DownloadErrors.NotFound(taskId));

        task.Priority = priority;
        await _repository.UpdateAsync(task, ct);
        await _scheduler.ReprioritizeAsync(taskId, priority, ct);
        _logger.Information("任务 {TaskId} 优先级调整为 {Priority}", taskId, priority);

        return Result.Ok();
    }
}
