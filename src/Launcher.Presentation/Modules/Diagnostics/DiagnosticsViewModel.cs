// Copyright (c) Helsincy. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.Diagnostics.Contracts;
using Serilog;

namespace Launcher.Presentation.Modules.Diagnostics;

/// <summary>
/// 诊断页面 ViewModel。管理系统信息采集和显示。
/// </summary>
public partial class DiagnosticsViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<DiagnosticsViewModel>();
    private readonly IDiagnosticsReadService _diagnosticsService;

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

    // === 状态 ===
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _lastRefreshed = string.Empty;

    public DiagnosticsViewModel(IDiagnosticsReadService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
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

    private static string FormatSize(long mb)
    {
        if (mb >= 1024)
            return $"{mb / 1024.0:F1} GB";
        return $"{mb} MB";
    }
}
