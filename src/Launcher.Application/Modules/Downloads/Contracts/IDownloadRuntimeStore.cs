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
}
