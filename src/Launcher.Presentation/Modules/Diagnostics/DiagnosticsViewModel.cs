// Copyright (c) Helsincy. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.Diagnostics.Contracts;
using Serilog;

namespace Launcher.Presentation.Modules.Diagnostics;

/// <summary>
/// 诊断页面 ViewModel。管理系统信息采集、日志查看和缓存管理。
/// </summary>
public partial class DiagnosticsViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<DiagnosticsViewModel>();
    private readonly IDiagnosticsReadService _diagnosticsService;
    private readonly ICacheManager _cacheManager;

    // === 系统信息 ===
    [ObservableProperty] private string _osVersion = string.Empty;
    [ObservableProperty] private string _dotNetVersion = string.Empty;
    [ObservableProperty] private string _appVersion = string.Empty;
    [ObservableProperty] private string _uptime = string.Empty;

    // === 磁盘空间 ===
    [ObservableProperty] private long _availableDiskSpaceMb;
    [ObservableProperty] private long _totalDiskSpaceMb;
    [ObservableProperty] private double _diskUsagePercent;
    [ObservableProperty] private string _diskUsageText = string.Empty;

    // === 内存 ===
    [ObservableProperty] private long _totalMemoryMb;
    [ObservableProperty] private long _processMemoryMb;
    [ObservableProperty] private string _memoryUsageText = string.Empty;

    // === 数据库 ===
    [ObservableProperty] private long _databaseSizeMb;

    // === 日志查看器 ===
    [ObservableProperty] private string _logSearchKeyword = string.Empty;
    [ObservableProperty] private int _selectedLogLevelIndex; // 0=全部, 1=Debug, 2=Info, 3=Warning, 4=Error
    [ObservableProperty] private bool _isLoadingLogs;
    [ObservableProperty] private int _logCount;

    public ObservableCollection<LogEntryDisplay> LogEntries { get; } = [];

    // === 缓存管理 ===
    [ObservableProperty] private string _thumbnailCacheSize = "0 B";
    [ObservableProperty] private int _thumbnailFileCount;
    [ObservableProperty] private string _manifestCacheSize = "0 B";
    [ObservableProperty] private int _manifestFileCount;
    [ObservableProperty] private string _logCacheSize = "0 B";
    [ObservableProperty] private int _logFileCount;
    [ObservableProperty] private string _totalCacheSize = "0 B";
    [ObservableProperty] private bool _isLoadingCache;
    [ObservableProperty] private string _cacheStatusMessage = string.Empty;

    // === 状态 ===
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _lastRefreshed = string.Empty;

    public DiagnosticsViewModel(IDiagnosticsReadService diagnosticsService, ICacheManager cacheManager)
    {
        _diagnosticsService = diagnosticsService;
        _cacheManager = cacheManager;
        Logger.Debug("DiagnosticsViewModel 已创建");
    }

    /// <summary>
    /// 刷新系统诊断信息
    /// </summary>
    [RelayCommand]
    private async Task RefreshSystemInfoAsync()
    {
        IsLoading = true;

        try
        {
            var summary = await _diagnosticsService.GetSystemSummaryAsync();

            OsVersion = summary.OsVersion;
            DotNetVersion = summary.DotNetVersion;
            AppVersion = summary.AppVersion;

            var uptimeSpan = DateTime.UtcNow - summary.AppStartedAt;
            Uptime = uptimeSpan.TotalHours >= 1
                ? $"{(int)uptimeSpan.TotalHours} 小时 {uptimeSpan.Minutes} 分钟"
                : $"{uptimeSpan.Minutes} 分钟 {uptimeSpan.Seconds} 秒";

            AvailableDiskSpaceMb = summary.AvailableDiskSpaceMb;
            TotalDiskSpaceMb = summary.TotalDiskSpaceMb;
            if (summary.TotalDiskSpaceMb > 0)
            {
                DiskUsagePercent = 100.0 * (summary.TotalDiskSpaceMb - summary.AvailableDiskSpaceMb) / summary.TotalDiskSpaceMb;
                DiskUsageText = $"{FormatSize(summary.AvailableDiskSpaceMb)} 可用 / {FormatSize(summary.TotalDiskSpaceMb)} 总计";
            }

            TotalMemoryMb = summary.TotalMemoryMb;
            ProcessMemoryMb = summary.ProcessMemoryMb;
            MemoryUsageText = $"进程占用 {summary.ProcessMemoryMb} MB / 系统总计 {FormatSize(summary.TotalMemoryMb)}";

            DatabaseSizeMb = summary.DatabaseSizeMb;

            LastRefreshed = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
            Logger.Information("系统诊断信息已刷新");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "刷新系统诊断信息失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 加载/刷新日志
    /// </summary>
    [RelayCommand]
    private async Task LoadLogsAsync()
    {
        IsLoadingLogs = true;

        try
        {
            var minLevel = SelectedLogLevelIndex switch
            {
                1 => LogEntryLevel.Debug,
                2 => LogEntryLevel.Information,
                3 => LogEntryLevel.Warning,
                4 => LogEntryLevel.Error,
                _ => (LogEntryLevel?)null,
            };

            IReadOnlyList<LogEntry> entries;
            if (!string.IsNullOrWhiteSpace(LogSearchKeyword))
            {
                entries = await _diagnosticsService.SearchLogsAsync(LogSearchKeyword, minLevel);
            }
            else
            {
                entries = await _diagnosticsService.GetRecentLogsAsync(500, minLevel);
            }

            LogEntries.Clear();
            foreach (var entry in entries)
            {
                LogEntries.Add(new LogEntryDisplay
                {
                    Timestamp = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
                    Level = entry.Level.ToString(),
                    LevelColor = GetLevelColor(entry.Level),
                    Source = entry.Source,
                    Message = entry.Message,
                    Exception = entry.Exception,
                    CorrelationId = entry.CorrelationId,
                    HasException = entry.Exception is not null,
                });
            }

            LogCount = LogEntries.Count;
            Logger.Debug("日志已加载 | 总计={Count} | 关键字={Keyword} | 级别={Level}",
                LogCount, LogSearchKeyword, minLevel);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "载入日志失败");
        }
        finally
        {
            IsLoadingLogs = false;
        }
    }

    /// <summary>
    /// 导出日志到文件（日志内容复制到剪贴板的简化版本）
    /// </summary>
    [RelayCommand]
    private async Task ExportLogsAsync()
    {
        try
        {
            var entries = await _diagnosticsService.GetRecentLogsAsync(2000);
            var lines = entries.Select(e =>
                $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{e.Level}] [{e.Source}] {e.Message}" +
                (e.Exception is not null ? $"\n  {e.Exception}" : string.Empty));

            ExportedLogText = string.Join(Environment.NewLine, lines);
            Logger.Information("日志已导出 | 总计={Count} 条", entries.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "导出日志失败");
        }
    }

    /// <summary>
    /// 导出的日志文本（供页面获取后写入文件或剪贴板）
    /// </summary>
    [ObservableProperty] private string _exportedLogText = string.Empty;

    private static string GetLevelColor(LogEntryLevel level) => level switch
    {
        LogEntryLevel.Debug => "#808080",
        LogEntryLevel.Information => "#0078D4",
        LogEntryLevel.Warning => "#CA8A04",
        LogEntryLevel.Error => "#DC2626",
        LogEntryLevel.Fatal => "#9B1C1C",
        _ => "#808080",
    };

    private static string FormatSize(long mb)
    {
        if (mb >= 1024)
            return $"{mb / 1024.0:F1} GB";
        return $"{mb} MB";
    }

    /// <summary>
    /// 刷新缓存统计信息
    /// </summary>
    [RelayCommand]
    private async Task RefreshCacheAsync()
    {
        IsLoadingCache = true;
        CacheStatusMessage = string.Empty;

        try
        {
            var stats = await _cacheManager.GetCacheStatisticsAsync();

            ThumbnailCacheSize = FormatBytes(stats.ThumbnailCacheBytes);
            ThumbnailFileCount = stats.ThumbnailFileCount;
            ManifestCacheSize = FormatBytes(stats.ManifestCacheBytes);
            ManifestFileCount = stats.ManifestFileCount;
            LogCacheSize = FormatBytes(stats.LogFileBytes);
            LogFileCount = stats.LogFileCount;
            TotalCacheSize = FormatBytes(stats.TotalBytes);

            Logger.Debug("缓存统计已刷新 | 总计={Total}", TotalCacheSize);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "刷新缓存统计失败");
        }
        finally
        {
            IsLoadingCache = false;
        }
    }

    /// <summary>
    /// 清理缩略图缓存
    /// </summary>
    [RelayCommand]
    private async Task ClearThumbnailCacheAsync()
    {
        IsLoadingCache = true;
        var result = await _cacheManager.ClearThumbnailCacheAsync();
        CacheStatusMessage = result.IsSuccess ? "缩略图缓存已清理" : "清理失败：" + result.Error?.UserMessage;
        await RefreshCacheAsync();
    }

    /// <summary>
    /// 清理 Manifest 缓存
    /// </summary>
    [RelayCommand]
    private async Task ClearManifestCacheAsync()
    {
        IsLoadingCache = true;
        var result = await _cacheManager.ClearManifestCacheAsync();
        CacheStatusMessage = result.IsSuccess ? "Manifest 缓存已清理" : "清理失败：" + result.Error?.UserMessage;
        await RefreshCacheAsync();
    }

    /// <summary>
    /// 清理日志缓存
    /// </summary>
    [RelayCommand]
    private async Task ClearLogCacheAsync()
    {
        IsLoadingCache = true;
        var result = await _cacheManager.ClearLogCacheAsync();
        CacheStatusMessage = result.IsSuccess ? "日志缓存已清理（保留最近 1 天）" : "清理失败：" + result.Error?.UserMessage;
        await RefreshCacheAsync();
    }

    /// <summary>
    /// 清理全部缓存
    /// </summary>
    [RelayCommand]
    private async Task ClearAllCacheAsync()
    {
        IsLoadingCache = true;
        var result = await _cacheManager.ClearAllCacheAsync();
        CacheStatusMessage = result.IsSuccess ? "全部缓存已清理" : "部分清理失败：" + result.Error?.UserMessage;
        await RefreshCacheAsync();
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1073741824L => $"{bytes / 1073741824.0:F2} GB",
            >= 1048576L => $"{bytes / 1048576.0:F1} MB",
            >= 1024L => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B",
        };
    }
}

/// <summary>
/// 日志条目显示模型（供 UI 绑定）
/// </summary>
public sealed class LogEntryDisplay
{
    public string Timestamp { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public string LevelColor { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
    public string? CorrelationId { get; init; }
    public bool HasException { get; init; }
}
