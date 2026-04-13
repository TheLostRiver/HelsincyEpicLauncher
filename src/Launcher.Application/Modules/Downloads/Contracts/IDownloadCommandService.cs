// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Downloads;
using Launcher.Shared;

namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载命令服务。对外暴露的写操作入口。
/// </summary>
public interface IDownloadCommandService
{
    Task<Result<DownloadTaskId>> StartAsync(StartDownloadRequest request, CancellationToken ct = default);
    Task<Result> PauseAsync(DownloadTaskId taskId, CancellationToken ct = default);
    Task<Result> ResumeAsync(DownloadTaskId taskId, CancellationToken ct = default);
    Task<Result> CancelAsync(DownloadTaskId taskId, CancellationToken ct = default);
    Task<Result> SetPriorityAsync(DownloadTaskId taskId, int priority, CancellationToken ct = default);
}
