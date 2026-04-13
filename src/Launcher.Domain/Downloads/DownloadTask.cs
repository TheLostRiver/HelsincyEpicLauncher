// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Common;
using Launcher.Shared;

namespace Launcher.Domain.Downloads;

/// <summary>
/// 下载任务聚合根。
/// 封装状态机、进度与元数据。
/// </summary>
public sealed class DownloadTask : Entity<DownloadTaskId>
{
    private readonly DownloadStateMachine _stateMachine;

    /// <summary>游戏资源标识</summary>
    public string AssetId { get; private set; }

    /// <summary>游戏显示名称</summary>
    public string DisplayName { get; private set; }

    /// <summary>下载地址</summary>
    public string DownloadUrl { get; private set; }

    /// <summary>目标安装路径</summary>
    public string InstallPath { get; private set; }

    /// <summary>总字节数</summary>
    public long TotalBytes { get; private set; }

    /// <summary>已下载字节数</summary>
    public long DownloadedBytes { get; private set; }

    /// <summary>当前下载速度（字节/秒）</summary>
    public long SpeedBytesPerSecond { get; private set; }

    /// <summary>优先级（越小越高）</summary>
    public int Priority { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>最后更新时间</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>当前内部状态</summary>
    public DownloadState State => _stateMachine.Current;

    /// <summary>当前 UI 状态</summary>
    public DownloadUiState UiState => DownloadStateMachine.MapToUiState(State);

    /// <summary>下载进度百分比 0-100</summary>
    public double ProgressPercent => TotalBytes > 0
        ? Math.Round((double)DownloadedBytes / TotalBytes * 100, 2)
        : 0;

    /// <summary>最后一次失败的错误信息</summary>
    public string? LastError { get; private set; }

    /// <summary>重试次数</summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// 创建新的下载任务
    /// </summary>
    public DownloadTask(DownloadTaskId id, string assetId, string displayName, string downloadUrl, string installPath, long totalBytes, int priority = 0)
    {
        Id = id;
        AssetId = assetId ?? throw new ArgumentNullException(nameof(assetId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        DownloadUrl = downloadUrl ?? throw new ArgumentNullException(nameof(downloadUrl));
        InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
        TotalBytes = totalBytes;
        Priority = priority;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        _stateMachine = new DownloadStateMachine();
    }

    /// <summary>
    /// 从持久化数据恢复下载任务
    /// </summary>
    public DownloadTask(
        DownloadTaskId id,
        string assetId,
        string displayName,
        string downloadUrl,
        string installPath,
        long totalBytes,
        long downloadedBytes,
        DownloadState state,
        int priority,
        int retryCount,
        string? lastError,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        AssetId = assetId;
        DisplayName = displayName;
        DownloadUrl = downloadUrl;
        InstallPath = installPath;
        TotalBytes = totalBytes;
        DownloadedBytes = downloadedBytes;
        Priority = priority;
        RetryCount = retryCount;
        LastError = lastError;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        _stateMachine = new DownloadStateMachine(state);
    }

    /// <summary>尝试转换状态</summary>
    public Result TransitionTo(DownloadState target)
    {
        var result = _stateMachine.TransitionTo(target);
        if (result.IsSuccess)
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }
        return result;
    }

    /// <summary>检查是否可以转换到目标状态</summary>
    public bool CanTransitionTo(DownloadState target)
        => _stateMachine.CanTransitionTo(target);

    /// <summary>更新下载进度</summary>
    public void UpdateProgress(long downloadedBytes, long speedBytesPerSecond)
    {
        DownloadedBytes = downloadedBytes;
        SpeedBytesPerSecond = speedBytesPerSecond;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>设置总字节数（获取清单后）</summary>
    public void SetTotalBytes(long totalBytes)
    {
        TotalBytes = totalBytes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>记录失败信息</summary>
    public void SetError(string error)
    {
        LastError = error;
        RetryCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>清除错误（重试时）</summary>
    public void ClearError()
    {
        LastError = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
