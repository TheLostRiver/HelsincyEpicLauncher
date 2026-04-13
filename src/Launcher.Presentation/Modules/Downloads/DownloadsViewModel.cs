// Copyright (c) Helsincy. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Serilog;

namespace Launcher.Presentation.Modules.Downloads;

/// <summary>
/// 下载管理页面 ViewModel。订阅 RuntimeStore 事件驱动实时 UI 更新。
/// </summary>
public partial class DownloadsViewModel : ObservableObject, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<DownloadsViewModel>();

    private readonly IDownloadReadService _readService;
    private readonly IDownloadCommandService _commandService;
    private readonly IDownloadRuntimeStore _runtimeStore;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private bool _disposed;

    /// <summary>
    /// 活跃下载列表
    /// </summary>
    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = [];

    /// <summary>
    /// 历史下载列表（已完成/失败/取消）
    /// </summary>
    public ObservableCollection<DownloadItemViewModel> History { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasActiveDownloads;
    [ObservableProperty] private bool _hasHistory;
    [ObservableProperty] private string _totalSpeedText = string.Empty;
    [ObservableProperty] private int _activeCount;

    public DownloadsViewModel(
        IDownloadReadService readService,
        IDownloadCommandService commandService,
        IDownloadRuntimeStore runtimeStore)
    {
        _readService = readService;
        _commandService = commandService;
        _runtimeStore = runtimeStore;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // 订阅实时事件
        _runtimeStore.SnapshotChanged += OnSnapshotChanged;
        _runtimeStore.DownloadCompleted += OnDownloadCompleted;
        _runtimeStore.DownloadFailed += OnDownloadFailed;

        Logger.Debug("DownloadsViewModel 已创建");
    }

    /// <summary>
    /// 页面加载时刷新列表
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var active = await _readService.GetActiveDownloadsAsync(CancellationToken.None);
            Downloads.Clear();
            foreach (var item in active)
                Downloads.Add(DownloadItemViewModel.FromSummary(item));

            var history = await _readService.GetHistoryAsync(50, CancellationToken.None);
            History.Clear();
            foreach (var item in history)
                History.Add(DownloadItemViewModel.FromSummary(item));

            UpdateAggregates();
            Logger.Information("下载列表已加载：活跃 {Active}，历史 {History}", Downloads.Count, History.Count);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PauseAsync(DownloadTaskId taskId)
    {
        var result = await _commandService.PauseAsync(taskId, CancellationToken.None);
        if (!result.IsSuccess)
            Logger.Warning("暂停失败: {TaskId}, {Error}", taskId, result.Error?.TechnicalMessage);
    }

    [RelayCommand]
    private async Task ResumeAsync(DownloadTaskId taskId)
    {
        var result = await _commandService.ResumeAsync(taskId, CancellationToken.None);
        if (!result.IsSuccess)
            Logger.Warning("恢复失败: {TaskId}, {Error}", taskId, result.Error?.TechnicalMessage);
    }

    [RelayCommand]
    private async Task CancelAsync(DownloadTaskId taskId)
    {
        var result = await _commandService.CancelAsync(taskId, CancellationToken.None);
        if (!result.IsSuccess)
            Logger.Warning("取消失败: {TaskId}, {Error}", taskId, result.Error?.TechnicalMessage);
    }

    private void OnSnapshotChanged(DownloadProgressSnapshot snapshot)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var existing = FindDownloadItem(snapshot.TaskId);
            if (existing is not null)
            {
                existing.UpdateFromSnapshot(snapshot);
            }
            UpdateAggregates();
        });
    }

    private void OnDownloadCompleted(DownloadCompletedEvent evt)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var item = FindDownloadItem(evt.TaskId);
            if (item is not null)
            {
                Downloads.Remove(item);
                item.UiState = DownloadUiState.Completed;
                History.Insert(0, item);
            }
            UpdateAggregates();
            Logger.Information("UI 收到下载完成通知: {TaskId}", evt.TaskId);
        });
    }

    private void OnDownloadFailed(DownloadFailedEvent evt)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var item = FindDownloadItem(evt.TaskId);
            if (item is not null)
            {
                item.UiState = DownloadUiState.Failed;
                item.ErrorMessage = evt.ErrorMessage;
            }
            UpdateAggregates();
            Logger.Warning("UI 收到下载失败通知: {TaskId}, {Error}", evt.TaskId, evt.ErrorMessage);
        });
    }

    private DownloadItemViewModel? FindDownloadItem(DownloadTaskId taskId)
    {
        return Downloads.FirstOrDefault(d => d.TaskId == taskId);
    }

    private void UpdateAggregates()
    {
        ActiveCount = Downloads.Count;
        HasActiveDownloads = Downloads.Count > 0;
        HasHistory = History.Count > 0;

        var totalSpeed = Downloads.Sum(d => d.SpeedBytesPerSecond);
        TotalSpeedText = totalSpeed > 0 ? $"{FormatSpeed(totalSpeed)}" : string.Empty;
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        return bytesPerSecond switch
        {
            >= 1073741824L => $"{bytesPerSecond / 1073741824.0:F2} GB/s",
            >= 1048576L => $"{bytesPerSecond / 1048576.0:F1} MB/s",
            >= 1024L => $"{bytesPerSecond / 1024.0:F1} KB/s",
            _ => $"{bytesPerSecond} B/s",
        };
    }

    internal static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1073741824L => $"{bytes / 1073741824.0:F2} GB",
            >= 1048576L => $"{bytes / 1048576.0:F1} MB",
            >= 1024L => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B",
        };
    }

    internal static string FormatTimeSpan(TimeSpan? ts)
    {
        if (ts is null) return "--";
        var t = ts.Value;
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}";
        if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
        return $"{t.Seconds}秒";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _runtimeStore.SnapshotChanged -= OnSnapshotChanged;
        _runtimeStore.DownloadCompleted -= OnDownloadCompleted;
        _runtimeStore.DownloadFailed -= OnDownloadFailed;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 单个下载任务的 UI 展示模型
/// </summary>
public partial class DownloadItemViewModel : ObservableObject
{
    [ObservableProperty] private DownloadTaskId _taskId;
    [ObservableProperty] private string _assetName = string.Empty;
    [ObservableProperty] private DownloadUiState _uiState;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private long _downloadedBytes;
    [ObservableProperty] private long _totalBytes;
    [ObservableProperty] private long _speedBytesPerSecond;
    [ObservableProperty] private string _speedText = string.Empty;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private string _etaText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _canPause;
    [ObservableProperty] private bool _canResume;
    [ObservableProperty] private bool _canCancel;

    public static DownloadItemViewModel FromSummary(DownloadStatusSummary summary)
    {
        var vm = new DownloadItemViewModel
        {
            TaskId = summary.TaskId,
            AssetName = summary.AssetName,
            UiState = summary.UiState,
            ProgressPercent = summary.Progress * 100,
            DownloadedBytes = summary.DownloadedBytes,
            TotalBytes = summary.TotalBytes,
            SpeedBytesPerSecond = summary.BytesPerSecond,
            CanPause = summary.CanPause,
            CanResume = summary.CanResume,
            CanCancel = summary.CanCancel,
            ErrorMessage = summary.ErrorMessage,
        };
        vm.UpdateDisplayTexts(summary.EstimatedRemaining);
        return vm;
    }

    public void UpdateFromSnapshot(DownloadProgressSnapshot snapshot)
    {
        UiState = snapshot.UiState;
        ProgressPercent = snapshot.ProgressPercent;
        DownloadedBytes = snapshot.DownloadedBytes;
        TotalBytes = snapshot.TotalBytes;
        SpeedBytesPerSecond = snapshot.SpeedBytesPerSecond;
        UpdateDisplayTexts(snapshot.EstimatedRemaining);
    }

    private void UpdateDisplayTexts(TimeSpan? eta)
    {
        SpeedText = SpeedBytesPerSecond > 0 ? FormatSpeed(SpeedBytesPerSecond) : string.Empty;
        ProgressText = TotalBytes > 0
            ? $"{DownloadsViewModel.FormatBytes(DownloadedBytes)} / {DownloadsViewModel.FormatBytes(TotalBytes)}"
            : DownloadsViewModel.FormatBytes(DownloadedBytes);
        EtaText = DownloadsViewModel.FormatTimeSpan(eta);
        StatusText = UiState switch
        {
            DownloadUiState.Queued => "排队中",
            DownloadUiState.Downloading => SpeedText.Length > 0 ? $"{SpeedText} · 剩余 {EtaText}" : "下载中...",
            DownloadUiState.Paused => "已暂停",
            DownloadUiState.Verifying => "校验中...",
            DownloadUiState.Completed => "已完成",
            DownloadUiState.Failed => ErrorMessage ?? "下载失败",
            DownloadUiState.Cancelled => "已取消",
            _ => string.Empty,
        };
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        return bytesPerSecond switch
        {
            >= 1073741824L => $"{bytesPerSecond / 1073741824.0:F2} GB/s",
            >= 1048576L => $"{bytesPerSecond / 1048576.0:F1} MB/s",
            >= 1024L => $"{bytesPerSecond / 1024.0:F1} KB/s",
            _ => $"{bytesPerSecond} B/s",
        };
    }
}
