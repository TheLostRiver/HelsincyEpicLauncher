// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Downloads;

namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载运行时状态存储接口。提供实时进度快照和事件订阅。
/// </summary>
public interface IDownloadRuntimeStore
{
    /// <summary>
    /// 进度快照变更事件（500ms 节流后触发）
    /// </summary>
    event Action<DownloadProgressSnapshot>? SnapshotChanged;

    /// <summary>
    /// 下载完成事件
    /// </summary>
    event Action<DownloadCompletedEvent>? DownloadCompleted;

    /// <summary>
    /// 下载失败事件
    /// </summary>
    event Action<DownloadFailedEvent>? DownloadFailed;

    /// <summary>
    /// 获取指定任务的最新快照
    /// </summary>
    DownloadProgressSnapshot? GetSnapshot(DownloadTaskId taskId);

    /// <summary>
    /// 获取所有活跃快照
    /// </summary>
    IReadOnlyList<DownloadProgressSnapshot> GetAllSnapshots();

    /// <summary>
    /// 更新任务进度（由下载执行器调用，内部实现可做节流）。
    /// </summary>
    void UpdateProgress(DownloadTaskId taskId, DownloadUiState uiState, long downloadedBytes, long totalBytes);

    /// <summary>
    /// 通知下载完成。
    /// </summary>
    void NotifyCompleted(DownloadTaskId taskId, string assetId, string filePath);

    /// <summary>
    /// 通知下载失败。
    /// </summary>
    void NotifyFailed(DownloadTaskId taskId, string assetId, string errorMessage, bool canRetry);

    /// <summary>
    /// 移除任务快照。
    /// </summary>
    void RemoveSnapshot(DownloadTaskId taskId);
}
