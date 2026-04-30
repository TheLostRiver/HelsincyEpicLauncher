// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Downloads;

namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载调度器接口。优先级队列 + 并发控制。
/// </summary>
public interface IDownloadScheduler : IDisposable
{
    /// <summary>最大并行任务数，运行时可调</summary>
    int MaxConcurrency { get; set; }

    /// <summary>当前活跃任务数</summary>
    int ActiveCount { get; }

    /// <summary>当有空位可调度时触发</summary>
    event Func<DownloadTaskId, CancellationToken, Task>? TaskReady;

    /// <summary>入队任务</summary>
    Task QueueAsync(DownloadTaskId taskId, int priority, CancellationToken ct);

    /// <summary>动态调整优先级</summary>
    Task ReprioritizeAsync(DownloadTaskId taskId, int priority, CancellationToken ct);

    /// <summary>获取当前活跃任务 ID 列表</summary>
    Task<IReadOnlyList<DownloadTaskId>> GetActiveTaskIdsAsync(CancellationToken ct);

    /// <summary>从队列移除任务</summary>
    void Dequeue(DownloadTaskId taskId);

    /// <summary>请求暂停任务</summary>
    void RequestPause(DownloadTaskId taskId);

    /// <summary>任务完成时调用（释放活跃位，调度下一个）</summary>
    void NotifyCompleted(DownloadTaskId taskId);
}

/// <summary>
/// 下载任务执行端口。调度器只分发任务 ID，具体执行器负责读取任务、下载 chunk 并回写状态。
/// </summary>
public interface IDownloadTaskExecutor
{
    Task ExecuteAsync(DownloadTaskId taskId, CancellationToken ct);
}
