// Copyright (c) Helsincy. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.Settings.Contracts;
using Microsoft.UI.Xaml;
using Serilog;

namespace Launcher.Presentation.Modules.Settings;

/// <summary>
/// 设置页面 ViewModel。管理各配置分组的双向绑定和保存/重置操作。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<SettingsViewModel>();
    private readonly ISettingsReadService _readService;
    private readonly ISettingsCommandService _commandService;

    // === 下载配置 ===
    [ObservableProperty] private int _maxConcurrentDownloads;
    [ObservableProperty] private int _maxChunksPerDownload;
    [ObservableProperty] private bool _autoInstall;

    // === 外观配置 ===
    [ObservableProperty] private int _selectedThemeIndex;
    [ObservableProperty] private string _language = "zh-CN";

    // === 路径配置 ===
    [ObservableProperty] private string _downloadPath = string.Empty;
    [ObservableProperty] private string _installPath = string.Empty;
    [ObservableProperty] private string _cachePath = string.Empty;

    // === 网络配置 ===
    [ObservableProperty] private string _proxyAddress = string.Empty;
    [ObservableProperty] private int _timeoutSeconds;
    [ObservableProperty] private bool _enableCdnFallback;

    // === Fab 配置 ===
    [ObservableProperty] private bool _autoWarmOnStartup;

    // === 状态 ===
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>
    /// 主题切换事件。Settings 页面监听此事件以通知 ThemeService 实时切换。
    /// </summary>
    public event Action<string>? ThemeChangeRequested;

    public SettingsViewModel(ISettingsReadService readService, ISettingsCommandService commandService)
    {
        _readService = readService;
        _commandService = commandService;

        LoadSettings();
        Logger.Debug("SettingsViewModel 已创建");
    }

    /// <summary>
    /// 从服务加载当前配置到属性
    /// </summary>
    private void LoadSettings()
    {
        var download = _readService.GetDownloadConfig();
        MaxConcurrentDownloads = download.MaxConcurrentDownloads;
        MaxChunksPerDownload = download.MaxChunksPerDownload;
        AutoInstall = download.AutoInstall;

        var appearance = _readService.GetAppearanceConfig();
        SelectedThemeIndex = appearance.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0,  // System
        };
        Language = appearance.Language;

        var paths = _readService.GetPathConfig();
        DownloadPath = paths.DownloadPath;
        InstallPath = paths.InstallPath;
        CachePath = paths.CachePath;

        var network = _readService.GetNetworkConfig();
        ProxyAddress = network.ProxyAddress;
        TimeoutSeconds = network.TimeoutSeconds;
        EnableCdnFallback = network.EnableCdnFallback;

        var fabLibrary = _readService.GetFabLibraryConfig();
        AutoWarmOnStartup = fabLibrary.AutoWarmOnStartup;
    }

    /// <summary>
    /// 保存下载配置
    /// </summary>
    [RelayCommand]
    private async Task SaveDownloadConfigAsync()
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        var config = new DownloadConfig
        {
            MaxConcurrentDownloads = MaxConcurrentDownloads,
            MaxChunksPerDownload = MaxChunksPerDownload,
            AutoInstall = AutoInstall,
        };

        var result = await _commandService.UpdateDownloadConfigAsync(config, CancellationToken.None);
        if (result.IsSuccess)
        {
            StatusMessage = "下载配置已保存";
            Logger.Information("下载配置已保存");
        }
        else
        {
            StatusMessage = $"保存失败：{result.Error?.UserMessage}";
            Logger.Warning("下载配置保存失败 | {Error}", result.Error?.TechnicalMessage);
        }

        IsSaving = false;
    }

    /// <summary>
    /// 保存外观配置
    /// </summary>
    [RelayCommand]
    private async Task SaveAppearanceConfigAsync()
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        string theme = SelectedThemeIndex switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "System",
        };

        var config = new AppearanceConfig
        {
            Theme = theme,
            Language = Language,
        };

        var result = await _commandService.UpdateAppearanceConfigAsync(config, CancellationToken.None);
        if (result.IsSuccess)
        {
            StatusMessage = "外观配置已保存";
            Logger.Information("外观配置已保存 | Theme={Theme}", theme);

            // 通知实时切换主题
            ThemeChangeRequested?.Invoke(theme);
        }
        else
        {
            StatusMessage = $"保存失败：{result.Error?.UserMessage}";
        }

        IsSaving = false;
    }

    /// <summary>
    /// 保存路径配置
    /// </summary>
    [RelayCommand]
    private async Task SavePathConfigAsync()
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        var config = new PathConfig
        {
            DownloadPath = DownloadPath,
            InstallPath = InstallPath,
            CachePath = CachePath,
        };

        var result = await _commandService.UpdatePathConfigAsync(config, CancellationToken.None);
        if (result.IsSuccess)
        {
            StatusMessage = "路径配置已保存";
            Logger.Information("路径配置已保存");
        }
        else
        {
            StatusMessage = $"保存失败：{result.Error?.UserMessage}";
        }

        IsSaving = false;
    }

    /// <summary>
    /// 保存网络配置
    /// </summary>
    [RelayCommand]
    private async Task SaveNetworkConfigAsync()
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        var config = new NetworkConfig
        {
            ProxyAddress = ProxyAddress,
            TimeoutSeconds = TimeoutSeconds,
            EnableCdnFallback = EnableCdnFallback,
        };

        var result = await _commandService.UpdateNetworkConfigAsync(config, CancellationToken.None);
        if (result.IsSuccess)
        {
            StatusMessage = "网络配置已保存";
            Logger.Information("网络配置已保存");
        }
        else
        {
            StatusMessage = $"保存失败：{result.Error?.UserMessage}";
        }

        IsSaving = false;
    }

    /// <summary>
    /// 保存 Fab 列表预热配置
    /// </summary>
    [RelayCommand]
    private async Task SaveFabLibraryConfigAsync()
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        var config = new FabLibraryConfig
        {
            AutoWarmOnStartup = AutoWarmOnStartup,
        };

        var result = await _commandService.UpdateFabLibraryConfigAsync(config, CancellationToken.None);
        if (result.IsSuccess)
        {
            StatusMessage = "Fab 预热配置已保存";
            Logger.Information("Fab 预热配置已保存 | AutoWarmOnStartup={AutoWarmOnStartup}", AutoWarmOnStartup);
        }
        else
        {
            StatusMessage = $"保存失败：{result.Error?.UserMessage}";
            Logger.Warning("Fab 预热配置保存失败 | {Error}", result.Error?.TechnicalMessage);
        }

        IsSaving = false;
    }

    /// <summary>
    /// 重置所有配置到默认值
    /// </summary>
    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        var result = await _commandService.ResetToDefaultsAsync(CancellationToken.None);
        if (result.IsSuccess)
        {
            LoadSettings();
            StatusMessage = "已重置为默认配置";
            Logger.Information("配置已重置为默认值");

            // 重置后通知主题切换
            string theme = SelectedThemeIndex switch
            {
                1 => "Light",
                2 => "Dark",
                _ => "System",
            };
            ThemeChangeRequested?.Invoke(theme);
        }
        else
        {
            StatusMessage = $"重置失败：{result.Error?.UserMessage}";
        }

        IsSaving = false;
    }

    /// <summary>
    /// 主题选择变更时实时生效并保存
    /// </summary>
    partial void OnSelectedThemeIndexChanged(int value)
    {
        // 避免 LoadSettings 初始化时触发保存
        if (_readService is null) return;

        _ = SaveAppearanceConfigAsync();
    }
}
