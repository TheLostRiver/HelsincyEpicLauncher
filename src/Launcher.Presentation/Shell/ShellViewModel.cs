// Copyright (c) Helsincy. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Presentation.Shell.Navigation;
using Serilog;

namespace Launcher.Presentation.Shell;

/// <summary>
/// Shell 壳层 ViewModel。管理导航状态和全局 UI 状态。
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<ShellViewModel>();
    private readonly INavigationService _navigationService;

    // === 导航状态 ===
    [ObservableProperty] private string _currentRoute = string.Empty;
    [ObservableProperty] private bool _canGoBack;

    // === 全局状态（后续任务接入各服务） ===
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private int _activeDownloadCount;
    [ObservableProperty] private bool _isNetworkAvailable = true;

    public ShellViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        Logger.Debug("ShellViewModel 已创建");
    }

    [RelayCommand]
    private Task NavigateToFabLibrary() => NavigateAsync(NavigationRoute.FabLibrary);

    [RelayCommand]
    private Task NavigateToDownloads() => NavigateAsync(NavigationRoute.Downloads);

    [RelayCommand]
    private Task NavigateToEngineVersions() => NavigateAsync(NavigationRoute.EngineVersions);

    [RelayCommand]
    private Task NavigateToSettings() => NavigateAsync(NavigationRoute.Settings);

    [RelayCommand]
    private Task NavigateToDiagnostics() => NavigateAsync(NavigationRoute.Diagnostics);

    [RelayCommand]
    private async Task GoBack()
    {
        await _navigationService.GoBackAsync();
        UpdateNavigationState();
    }

    private async Task NavigateAsync(string route)
    {
        await _navigationService.NavigateAsync(route);
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        CurrentRoute = _navigationService.CurrentRoute;
        CanGoBack = _navigationService.CanGoBack;
    }
}
