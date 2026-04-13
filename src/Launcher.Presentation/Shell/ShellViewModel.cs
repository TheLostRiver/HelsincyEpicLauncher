// Copyright (c) Helsincy. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Presentation.Shell.Navigation;
using Serilog;

namespace Launcher.Presentation.Shell;

/// <summary>
/// Shell 壳层 ViewModel。管理导航状态、认证状态和全局 UI 状态。
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<ShellViewModel>();
    private readonly INavigationService _navigationService;
    private readonly IAuthService _authService;
    private readonly IDownloadRuntimeStore _runtimeStore;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    // === 导航状态 ===
    [ObservableProperty] private string _currentRoute = string.Empty;
    [ObservableProperty] private bool _canGoBack;

    // === 认证状态 ===
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _displayName = "未登录";
    [ObservableProperty] private string _accountId = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private bool _isLoggingIn;

    /// <summary>未登录状态（方便 x:Bind 绑定取反）</summary>
    public bool IsNotAuthenticated => !IsAuthenticated;

    partial void OnIsAuthenticatedChanged(bool value) => OnPropertyChanged(nameof(IsNotAuthenticated));

    // === 全局状态 ===
    [ObservableProperty] private int _activeDownloadCount;
    [ObservableProperty] private bool _isNetworkAvailable = true;
    [ObservableProperty] private string _downloadSpeedText = string.Empty;
    [ObservableProperty] private bool _hasActiveDownloads;

    public ShellViewModel(INavigationService navigationService, IAuthService authService, IDownloadRuntimeStore runtimeStore)
    {
        _navigationService = navigationService;
        _authService = authService;
        _runtimeStore = runtimeStore;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // 监听会话过期事件
        _authService.SessionExpired += OnSessionExpired;

        // 监听下载进度，更新状态栏
        _runtimeStore.SnapshotChanged += OnDownloadSnapshotChanged;
        _runtimeStore.DownloadCompleted += _ => RefreshDownloadStatus();
        _runtimeStore.DownloadFailed += _ => RefreshDownloadStatus();

        Logger.Debug("ShellViewModel 已创建");
    }

    /// <summary>
    /// 启动时尝试恢复会话
    /// </summary>
    [RelayCommand]
    private async Task TryRestoreSessionAsync()
    {
        var result = await _authService.TryRestoreSessionAsync();
        if (result.IsSuccess)
        {
            UpdateUserInfo(result.Value!);
            Logger.Information("会话已自动恢复 | 用户={Name}", result.Value!.DisplayName);
        }
        else
        {
            Logger.Debug("无可恢复的会话");
        }
    }

    /// <summary>
    /// 登录
    /// </summary>
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoggingIn) return;
        IsLoggingIn = true;

        try
        {
            var result = await _authService.LoginAsync();
            if (result.IsSuccess)
            {
                UpdateUserInfo(result.Value!);
                Logger.Information("登录成功 | 用户={Name}", result.Value!.DisplayName);
            }
            else
            {
                Logger.Warning("登录失败 | Error={Error}", result.Error?.UserMessage);
            }
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    /// <summary>
    /// 登出
    /// </summary>
    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        ClearUserInfo();
        Logger.Information("用户已登出");
    }

    private void UpdateUserInfo(AuthUserInfo user)
    {
        IsAuthenticated = true;
        DisplayName = user.DisplayName;
        AccountId = user.AccountId;
        Email = user.Email;
    }

    private void ClearUserInfo()
    {
        IsAuthenticated = false;
        DisplayName = "未登录";
        AccountId = string.Empty;
        Email = string.Empty;
    }

    private void OnSessionExpired(SessionExpiredEvent evt)
    {
        ClearUserInfo();
        Logger.Warning("会话已过期 | 原因={Reason}", evt.Reason);
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

    private void OnDownloadSnapshotChanged(DownloadProgressSnapshot _)
    {
        RefreshDownloadStatus();
    }

    private void RefreshDownloadStatus()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var snapshots = _runtimeStore.GetAllSnapshots();
            ActiveDownloadCount = snapshots.Count;
            HasActiveDownloads = snapshots.Count > 0;

            var totalSpeed = snapshots.Sum(s => s.SpeedBytesPerSecond);
            DownloadSpeedText = totalSpeed > 0 ? FormatSpeed(totalSpeed) : string.Empty;
        });
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
