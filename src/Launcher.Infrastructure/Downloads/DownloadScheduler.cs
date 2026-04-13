// Copyright (c) Helsincy. All rights reserved.

using System.Collections.Concurrent;
using Launcher.Domain.Downloads;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 下载调度器。优先级队列 + 并发控制（最大 3 个并行任务）。
/// </summary>
public sealed class DownloadScheduler : IDisposable
{
    private readonly SortedSet<QueueEntry> _queue = new(QueueEntryComparer.Instance);
    private readonly ConcurrentDictionary<DownloadTaskId, CancellationTokenSource> _activeTasks = new();
    private readonly Lock _queueLock = new();
    private readonly ILogger _logger = Log.ForContext<DownloadScheduler>();
    private bool _disposed;

    /// <summary>最大并行任务数，运行时可调</summary>
    public int MaxConcurrency { get; set; } = 3;

    /// <summary>当前活跃任务数</summary>
    public int ActiveCount => _activeTasks.Count;

    /// <summary>当有空位可调度时触发</summary>
    public event Func<DownloadTaskId, CancellationToken, Task>? TaskReady;

    /// <summary>
    /// 入队任务
    /// </summary>
    public Task QueueAsync(DownloadTaskId taskId, int priority, CancellationToken ct)
    {
        lock (_queueLock)
        {
            _queue.RemoveWhere(e => e.TaskId == taskId);
            _queue.Add(new QueueEntry(taskId, priority, DateTimeOffset.UtcNow));
        }

        _logger.Debug("任务 {TaskId} 入队, 优先级 {Priority}, 队列长度 {Count}",
            taskId, priority, _queue.Count);

        _ = TryScheduleNextAsync(ct);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 动态调整优先级
    /// </summary>
    public Task ReprioritizeAsync(DownloadTaskId taskId, int priority, CancellationToken ct)
    {
        lock (_queueLock)
        {
            var existing = _queue.FirstOrDefault(e => e.TaskId == taskId);
            if (existing != default)
            {
                _queue.Remove(existing);
                _queue.Add(existing with { Priority = priority });
                _logger.Debug("任务 {TaskId} 优先级调整为 {Priority}", taskId, priority);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取当前活跃任务 ID 列表
    /// </summary>
    public Task<IReadOnlyList<DownloadTaskId>> GetActiveTaskIdsAsync(CancellationToken ct)
    {
        IReadOnlyList<DownloadTaskId> result = _activeTasks.Keys.ToList();
        return Task.FromResult(result);
    }

    /// <summary>
    /// 从队列移除任务
    /// </summary>
    public void Dequeue(DownloadTaskId taskId)
    {
        lock (_queueLock)
        {
            _queue.RemoveWhere(e => e.TaskId == taskId);
        }

        if (_activeTasks.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger.Debug("活跃任务 {TaskId} 已取消并移除", taskId);
            _ = TryScheduleNextAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 请求暂停任务
    /// </summary>
    public void RequestPause(DownloadTaskId taskId)
    {
        if (_activeTasks.TryGetValue(taskId, out var cts))
        {
            cts.Cancel();
            _logger.Debug("请求暂停任务 {TaskId}", taskId);
        }
    }

    /// <summary>
    /// 任务完成时调用（释放活跃位，调度下一个）
    /// </summary>
    public void NotifyCompleted(DownloadTaskId taskId)
    {
        if (_activeTasks.TryRemove(taskId, out var cts))
        {
            cts.Dispose();
            _logger.Debug("任务 {TaskId} 完成, 释放调度位", taskId);
            _ = TryScheduleNextAsync(CancellationToken.None);
        }
    }

    private async Task TryScheduleNextAsync(CancellationToken ct)
    {
        while (_activeTasks.Count < MaxConcurrency)
        {
            DownloadTaskId nextTaskId;
            lock (_queueLock)
            {
                if (_queue.Count == 0) break;
                var next = _queue.Min!;
                _queue.Remove(next);
                nextTaskId = next.TaskId;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (!_activeTasks.TryAdd(nextTaskId, cts))
            {
                cts.Dispose();
                continue;
            }

            _logger.Information("调度任务 {TaskId}, 活跃数 {Active}/{Max}",
                nextTaskId, _activeTasks.Count, MaxConcurrency);

            if (TaskReady is not null)
            {
                try
                {
                    await TaskReady.Invoke(nextTaskId, cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "任务 {TaskId} 启动失败", nextTaskId);
                    _activeTasks.TryRemove(nextTaskId, out _);
                    cts.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _activeTasks)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        _activeTasks.Clear();
    }

    private sealed record QueueEntry(DownloadTaskId TaskId, int Priority, DateTimeOffset EnqueuedAt);

    private sealed class QueueEntryComparer : IComparer<QueueEntry>
    {
        public static readonly QueueEntryComparer Instance = new();

        public int Compare(QueueEntry? x, QueueEntry? y)
        {
            if (x is null || y is null) return 0;
            var priorityCompare = x.Priority.CompareTo(y.Priority);
            if (priorityCompare != 0) return priorityCompare;
            var timeCompare = x.EnqueuedAt.CompareTo(y.EnqueuedAt);
            if (timeCompare != 0) return timeCompare;
            return x.TaskId.Value.CompareTo(y.TaskId.Value);
        }
    }
}
