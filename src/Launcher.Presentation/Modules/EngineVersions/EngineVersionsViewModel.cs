// Copyright (c) Helsincy. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.EngineVersions.Contracts;
using Launcher.Shared.Configuration;
using Serilog;

namespace Launcher.Presentation.Modules.EngineVersions;

/// <summary>
/// 引擎版本管理页面 ViewModel。展示可用版本 + 已安装版本，提供下载/卸载/启动操作。
/// </summary>
public partial class EngineVersionsViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<EngineVersionsViewModel>();

    private readonly IEngineVersionReadService _readService;
    private readonly IEngineVersionCommandService _commandService;
    private readonly IAppConfigProvider _configProvider;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    /// <summary>可用引擎版本</summary>
    public ObservableCollection<EngineVersionItemViewModel> AvailableVersions { get; } = [];

    /// <summary>已安装引擎版本</summary>
    public ObservableCollection<InstalledEngineItemViewModel> InstalledVersions { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isNotLoading = true;
    [ObservableProperty] private bool _hasAvailable;
    [ObservableProperty] private bool _hasInstalled;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string? _errorMessage;

    partial void OnIsLoadingChanged(bool value)
    {
        IsNotLoading = !value;
        UpdateEmpty();
    }

    partial void OnErrorMessageChanged(string? value) => HasError = !string.IsNullOrEmpty(value);

    private void UpdateEmpty() => IsEmpty = !HasAvailable && !HasInstalled && !IsLoading;

    public EngineVersionsViewModel(
        IEngineVersionReadService readService,
        IEngineVersionCommandService commandService,
        IAppConfigProvider configProvider)
    {
        _readService = readService;
        _commandService = commandService;
        _configProvider = configProvider;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        Logger.Debug("EngineVersionsViewModel 已创建");
    }

    /// <summary>页面加载时刷新版本列表</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var availableTask = _readService.GetAvailableVersionsAsync(CancellationToken.None);
            var installedTask = _readService.GetInstalledVersionsAsync(CancellationToken.None);

            await Task.WhenAll(availableTask, installedTask);

            var availableResult = availableTask.Result;
            if (availableResult.IsSuccess)
            {
                AvailableVersions.Clear();
                foreach (var v in availableResult.Value!)
                    AvailableVersions.Add(new EngineVersionItemViewModel(v));
            }
            else
            {
                ErrorMessage = availableResult.Error?.UserMessage ?? "加载可用版本失败";
                Logger.Warning("加载可用版本失败: {Error}", availableResult.Error?.TechnicalMessage);
            }

            var installedResult = installedTask.Result;
            if (installedResult.IsSuccess)
            {
                InstalledVersions.Clear();
                foreach (var v in installedResult.Value!)
                    InstalledVersions.Add(new InstalledEngineItemViewModel(v));
            }
            else
            {
                Logger.Warning("加载已安装版本失败: {Error}", installedResult.Error?.TechnicalMessage);
            }

            HasAvailable = AvailableVersions.Count > 0;
            HasInstalled = InstalledVersions.Count > 0;
            UpdateEmpty();
            Logger.Information("引擎版本加载完成：可用 {Available}，已安装 {Installed}",
                AvailableVersions.Count, InstalledVersions.Count);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>下载并安装版本</summary>
    [RelayCommand]
    private async Task DownloadAsync(EngineVersionItemViewModel item)
    {
        if (item.IsDownloading) return;

        item.IsDownloading = true;
        try
        {
            var installPath = Path.Combine(
                _configProvider.InstallPath,
                "Epic Games",
                $"UE_{item.DisplayName}");

            var result = await _commandService.DownloadAndInstallAsync(item.VersionId, installPath, CancellationToken.None);
            if (result.IsSuccess)
            {
                item.StatusText = "下载中...";
                Logger.Information("引擎下载已发起: {VersionId}", item.VersionId);
            }
            else
            {
                item.StatusText = result.Error?.UserMessage ?? "下载失败";
                Logger.Warning("引擎下载失败: {VersionId}, {Error}", item.VersionId, result.Error?.TechnicalMessage);
            }
        }
        finally
        {
            item.IsDownloading = false;
        }
    }

    /// <summary>卸载引擎</summary>
    [RelayCommand]
    private async Task UninstallAsync(InstalledEngineItemViewModel item)
    {
        var result = await _commandService.UninstallAsync(item.VersionId, CancellationToken.None);
        if (result.IsSuccess)
        {
            InstalledVersions.Remove(item);
            HasInstalled = InstalledVersions.Count > 0;

            // 刷新可用列表中的安装状态
            var available = AvailableVersions.FirstOrDefault(v => v.VersionId == item.VersionId);
            if (available is not null)
                available.IsInstalled = false;

            Logger.Information("引擎已卸载: {VersionId}", item.VersionId);
        }
        else
        {
            Logger.Warning("引擎卸载失败: {VersionId}, {Error}", item.VersionId, result.Error?.TechnicalMessage);
        }
    }

    /// <summary>启动编辑器</summary>
    [RelayCommand]
    private async Task LaunchAsync(InstalledEngineItemViewModel item)
    {
        var result = await _commandService.LaunchEditorAsync(item.VersionId, CancellationToken.None);
        if (!result.IsSuccess)
        {
            Logger.Warning("启动编辑器失败: {VersionId}, {Error}", item.VersionId, result.Error?.TechnicalMessage);
        }
    }
}

/// <summary>可用引擎版本项</summary>
public partial class EngineVersionItemViewModel : ObservableObject
{
    public string VersionId { get; }
    public string DisplayName { get; }
    public string DownloadSizeText { get; }
    public string ReleaseDateText { get; }

    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _canDownload;

    public EngineVersionItemViewModel(EngineVersionSummary summary)
    {
        VersionId = summary.VersionId;
        DisplayName = summary.DisplayName;
        IsInstalled = summary.IsInstalled;
        CanDownload = !summary.IsInstalled;
        DownloadSizeText = FormatSize(summary.DownloadSize);
        ReleaseDateText = summary.ReleaseDate != default
            ? summary.ReleaseDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
    }

    partial void OnStatusTextChanged(string? value) => HasStatus = !string.IsNullOrEmpty(value);
    partial void OnIsInstalledChanged(bool value) => CanDownload = !value && !IsDownloading;
    partial void OnIsDownloadingChanged(bool value) => CanDownload = !IsInstalled && !value;

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1073741824L => $"{bytes / 1073741824.0:F2} GB",
        >= 1048576L => $"{bytes / 1048576.0:F1} MB",
        >= 1024L => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B",
    };
}

/// <summary>已安装引擎版本项</summary>
public partial class InstalledEngineItemViewModel : ObservableObject
{
    public string VersionId { get; }
    public string DisplayName { get; }
    public string InstallPath { get; }
    public string SizeOnDiskText { get; }
    public string InstalledAtText { get; }

    public InstalledEngineItemViewModel(InstalledEngineSummary summary)
    {
        VersionId = summary.VersionId;
        DisplayName = summary.DisplayName;
        InstallPath = summary.InstallPath;
        SizeOnDiskText = FormatSize(summary.SizeOnDisk);
        InstalledAtText = summary.InstalledAt != default
            ? summary.InstalledAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1073741824L => $"{bytes / 1073741824.0:F2} GB",
        >= 1048576L => $"{bytes / 1048576.0:F1} MB",
        >= 1024L => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B",
    };
}
