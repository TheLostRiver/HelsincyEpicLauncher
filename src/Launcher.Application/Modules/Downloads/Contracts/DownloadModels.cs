// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Downloads;

namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 创建下载任务的请求
/// </summary>
public sealed class StartDownloadRequest
{
    public required string AssetId { get; init; }
    public required string AssetName { get; init; }
    public required string DownloadUrl { get; init; }
    public required string DestinationPath { get; init; }
    public long TotalBytes { get; init; }
    public int Priority { get; init; }
}

/// <summary>
/// 下载任务公共标识。UI 和跨模块调用方使用该类型，避免直接依赖领域值对象。
/// </summary>
public readonly record struct DownloadTaskKey(Guid Value)
{
    public static DownloadTaskKey FromLegacy(DownloadTaskId taskId) => new(taskId.Value);

    public DownloadTaskId ToLegacyTaskId() => new(Value);

    public override string ToString() => Value.ToString();
}

/// <summary>
/// 下载状态摘要 DTO，对外展示唯一模型
/// </summary>
public sealed class DownloadStatusSummary
{
    public required DownloadTaskId TaskId { get; init; }
    public DownloadTaskKey TaskKey => DownloadTaskKey.FromLegacy(TaskId);
    public required string AssetId { get; init; }
    public required string AssetName { get; init; }
    public DownloadStatusKind Status { get; init; }
    public DownloadUiState UiState { get; init; }
    public double Progress { get; init; }
    public long DownloadedBytes { get; init; }
    public long TotalBytes { get; init; }
    public long BytesPerSecond { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    public bool CanPause { get; init; }
    public bool CanResume { get; init; }
    public bool CanCancel { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 下载状态公共投影。由 Application Contracts 拥有，供 UI 和跨模块调用方使用。
/// </summary>
public enum DownloadStatusKind
{
    Queued,
    Downloading,
    Paused,
    Verifying,
    Installing,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// 下载进度快照 DTO，用于实时进度更新
/// </summary>
public sealed record DownloadProgressSnapshot(
    DownloadTaskId TaskId,
    DownloadUiState UiState,
    double ProgressPercent,
    long DownloadedBytes,
    long TotalBytes,
    long SpeedBytesPerSecond,
    TimeSpan? EstimatedRemaining = null)
{
    public DownloadTaskKey TaskKey => DownloadTaskKey.FromLegacy(TaskId);

    public DownloadStatusKind Status => UiState switch
    {
        DownloadUiState.Queued => DownloadStatusKind.Queued,
        DownloadUiState.Downloading => DownloadStatusKind.Downloading,
        DownloadUiState.Paused => DownloadStatusKind.Paused,
        DownloadUiState.Verifying => DownloadStatusKind.Verifying,
        DownloadUiState.Completed => DownloadStatusKind.Completed,
        DownloadUiState.Failed => DownloadStatusKind.Failed,
        DownloadUiState.Cancelled => DownloadStatusKind.Cancelled,
        _ => DownloadStatusKind.Failed,
    };
}

// ===== 事件 =====

public sealed record DownloadCompletedEvent(
    DownloadTaskId TaskId, string AssetId, string DownloadedFilePath)
{
    public DownloadTaskKey TaskKey => DownloadTaskKey.FromLegacy(TaskId);
}

public sealed record DownloadFailedEvent(
    DownloadTaskId TaskId, string AssetId, string ErrorMessage, bool CanRetry)
{
    public DownloadTaskKey TaskKey => DownloadTaskKey.FromLegacy(TaskId);
}

public sealed record DownloadProgressChangedEvent(
    DownloadTaskId TaskId, double Progress, long BytesPerSecond)
{
    public DownloadTaskKey TaskKey => DownloadTaskKey.FromLegacy(TaskId);
}
