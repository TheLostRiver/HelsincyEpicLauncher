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
/// 下载状态摘要 DTO，对外展示唯一模型
/// </summary>
public sealed class DownloadStatusSummary
{
    public required DownloadTaskId TaskId { get; init; }
    public required string AssetId { get; init; }
    public required string AssetName { get; init; }
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
/// 下载进度快照 DTO，用于实时进度更新
/// </summary>
public sealed record DownloadProgressSnapshot(
    DownloadTaskId TaskId,
    DownloadUiState UiState,
    double ProgressPercent,
    long DownloadedBytes,
    long TotalBytes,
    long SpeedBytesPerSecond);

// ===== 事件 =====

public sealed record DownloadCompletedEvent(
    DownloadTaskId TaskId, string AssetId, string DownloadedFilePath);

public sealed record DownloadFailedEvent(
    DownloadTaskId TaskId, string AssetId, string ErrorMessage, bool CanRetry);

public sealed record DownloadProgressChangedEvent(
    DownloadTaskId TaskId, double Progress, long BytesPerSecond);
