// Copyright (c) Helsincy. All rights reserved.

using System.Collections.Concurrent;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 下载运行时状态存储。内存中维护进度快照，500ms 节流聚合。
/// </summary>
public sealed class DownloadRuntimeStore : IDownloadRuntimeStore, IDisposable
{
    private readonly ConcurrentDictionary<DownloadTaskId, DownloadProgressSnapshot> _snapshots = new();
    private readonly ConcurrentDictionary<DownloadTaskId, SpeedCalculator> _speedCalcs = new();
    private readonly ILogger _logger = Log.ForContext<DownloadRuntimeStore>();
    private bool _disposed;

    /// <summary>
    /// 进度快照变更事件（500ms 节流后触发）
    /// </summary>
    public event Action<DownloadProgressSnapshot>? SnapshotChanged;

    /// <summary>
    /// 下载完成事件
    /// </summary>
    public event Action<DownloadCompletedEvent>? DownloadCompleted;

    /// <summary>
    /// 下载失败事件
    /// </summary>
    public event Action<DownloadFailedEvent>? DownloadFailed;

    /// <summary>
    /// 更新任务进度（由 Worker 调用，内部节流）
    /// </summary>
    public void UpdateProgress(DownloadTaskId taskId, DownloadUiState uiState, long downloadedBytes, long totalBytes)
    {
        var speedCalc = _speedCalcs.GetOrAdd(taskId, _ => new SpeedCalculator());
        speedCalc.AddSample(downloadedBytes);
        var speed = speedCalc.GetSpeed();

        TimeSpan? eta = null;
        if (speed > 0 && totalBytes > downloadedBytes)
        {
            var remainingBytes = totalBytes - downloadedBytes;
            eta = TimeSpan.FromSeconds((double)remainingBytes / speed);
        }

        var snapshot = new DownloadProgressSnapshot(
            taskId,
            uiState,
            totalBytes > 0 ? Math.Round((double)downloadedBytes / totalBytes * 100, 2) : 0,
            downloadedBytes,
            totalBytes,
            speed,
            eta);

        _snapshots[taskId] = snapshot;

        // 节流：SpeedCalculator 累积到 500ms 才更新
        if (speedCalc.ShouldNotify())
        {
            SnapshotChanged?.Invoke(snapshot);
        }
    }

    /// <summary>
    /// 获取指定任务的最新快照
    /// </summary>
    public DownloadProgressSnapshot? GetSnapshot(DownloadTaskId taskId)
    {
        _snapshots.TryGetValue(taskId, out var snapshot);
        return snapshot;
    }

    /// <summary>
    /// 获取所有活跃快照
    /// </summary>
    public IReadOnlyList<DownloadProgressSnapshot> GetAllSnapshots()
    {
        return _snapshots.Values.ToList();
    }

    /// <summary>
    /// 通知下载完成
    /// </summary>
    public void NotifyCompleted(DownloadTaskId taskId, string assetId, string filePath)
    {
        _snapshots.TryRemove(taskId, out _);
        _speedCalcs.TryRemove(taskId, out _);
        DownloadCompleted?.Invoke(new DownloadCompletedEvent(taskId, assetId, filePath));
        _logger.Information("下载完成: {TaskId}, {AssetId}", taskId, assetId);
    }

    /// <summary>
    /// 通知下载失败
    /// </summary>
    public void NotifyFailed(DownloadTaskId taskId, string assetId, string errorMessage, bool canRetry)
    {
        _snapshots.TryRemove(taskId, out _);
        _speedCalcs.TryRemove(taskId, out _);
        DownloadFailed?.Invoke(new DownloadFailedEvent(taskId, assetId, errorMessage, canRetry));
        _logger.Warning("下载失败: {TaskId}, {AssetId}, {Error}", taskId, assetId, errorMessage);
    }

    /// <summary>
    /// 移除任务快照（暂停/取消时）
    /// </summary>
    public void RemoveSnapshot(DownloadTaskId taskId)
    {
        _snapshots.TryRemove(taskId, out _);
        _speedCalcs.TryRemove(taskId, out _);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _snapshots.Clear();
        _speedCalcs.Clear();
    }

    /// <summary>
    /// 滑动窗口速度计算器（5 秒窗口）+ 500ms 通知节流
    /// </summary>
    private sealed class SpeedCalculator
    {
        private readonly Queue<(DateTimeOffset Time, long Bytes)> _samples = new();
        private readonly TimeSpan _windowSize = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _notifyInterval = TimeSpan.FromMilliseconds(500);
        private DateTimeOffset _lastNotifyTime = DateTimeOffset.MinValue;
        private (DateTimeOffset Time, long Bytes) _newestSample;

        public void AddSample(long totalBytesDownloaded)
        {
            var now = DateTimeOffset.UtcNow;
            _newestSample = (now, totalBytesDownloaded);
            _samples.Enqueue(_newestSample);

            // 移除窗口外的样本
            while (_samples.Count > 0 && now - _samples.Peek().Time > _windowSize)
                _samples.Dequeue();
        }

        public long GetSpeed()
        {
            if (_samples.Count < 2) return 0;

            var oldest = _samples.Peek();
            var newest = _newestSample;
            var duration = (newest.Time - oldest.Time).TotalSeconds;

            if (duration <= 0) return 0;
            return (long)((newest.Bytes - oldest.Bytes) / duration);
        }

        public bool ShouldNotify()
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastNotifyTime < _notifyInterval)
                return false;
            _lastNotifyTime = now;
            return true;
        }
    }
}
