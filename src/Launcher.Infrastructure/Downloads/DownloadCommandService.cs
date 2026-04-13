// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 下载命令服务。对外命令入口，委托给 Orchestrator。
/// </summary>
public sealed class DownloadCommandService : IDownloadCommandService
{
    private readonly DownloadOrchestrator _orchestrator;
    private readonly ILogger _logger = Log.ForContext<DownloadCommandService>();

    public DownloadCommandService(DownloadOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<Result<DownloadTaskId>> StartAsync(StartDownloadRequest request, CancellationToken ct)
    {
        _logger.Information("开始下载: {AssetName} ({AssetId})", request.AssetName, request.AssetId);
        return await _orchestrator.EnqueueAsync(request, ct);
    }

    public async Task<Result> PauseAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        _logger.Information("暂停下载: {TaskId}", taskId);
        return await _orchestrator.PauseAsync(taskId, ct);
    }

    public async Task<Result> ResumeAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        _logger.Information("恢复下载: {TaskId}", taskId);
        return await _orchestrator.ResumeAsync(taskId, ct);
    }

    public async Task<Result> CancelAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        _logger.Information("取消下载: {TaskId}", taskId);
        return await _orchestrator.CancelAsync(taskId, ct);
    }

    public async Task<Result> SetPriorityAsync(DownloadTaskId taskId, int priority, CancellationToken ct)
    {
        return await _orchestrator.SetPriorityAsync(taskId, priority, ct);
    }
}
