// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Downloads.UseCases;
using Launcher.Domain.Downloads;
using Launcher.Shared;

namespace Launcher.Application.Modules.Downloads;

public sealed class DownloadCommandService : IDownloadCommandService
{
    private readonly StartDownloadUseCase _startDownloadUseCase;
    private readonly IDownloadOrchestrator _orchestrator;

    public DownloadCommandService(
        StartDownloadUseCase startDownloadUseCase,
        IDownloadOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(startDownloadUseCase);
        ArgumentNullException.ThrowIfNull(orchestrator);

        _startDownloadUseCase = startDownloadUseCase;
        _orchestrator = orchestrator;
    }

    public async Task<Result<DownloadTaskId>> StartAsync(StartDownloadRequest request, CancellationToken ct)
    {
        return await _startDownloadUseCase.ExecuteAsync(request, ct);
    }

    public async Task<Result> PauseAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        return await _orchestrator.PauseAsync(taskId, ct);
    }

    public async Task<Result> ResumeAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        return await _orchestrator.ResumeAsync(taskId, ct);
    }

    public async Task<Result> CancelAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        return await _orchestrator.CancelAsync(taskId, ct);
    }

    public async Task<Result> SetPriorityAsync(DownloadTaskId taskId, int priority, CancellationToken ct)
    {
        return await _orchestrator.SetPriorityAsync(taskId, priority, ct);
    }

    public async Task<Result> PauseAllAsync(CancellationToken ct)
    {
        var activeTaskIds = await _orchestrator.GetActiveTaskIdsAsync(ct);

        foreach (var taskId in activeTaskIds)
        {
            await _orchestrator.PauseAsync(taskId, ct);
        }

        return Result.Ok();
    }

    public async Task<Result> ResumeAllAsync(CancellationToken ct)
    {
        var pausedTaskIds = await _orchestrator.GetPausedTaskIdsAsync(ct);

        foreach (var taskId in pausedTaskIds)
        {
            await _orchestrator.ResumeAsync(taskId, ct);
        }

        return Result.Ok();
    }
}
