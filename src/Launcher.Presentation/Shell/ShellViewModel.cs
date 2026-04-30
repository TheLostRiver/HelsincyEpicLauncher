// Copyright (c) Helsincy. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Launcher.Application.Modules.Updates.Contracts;
using Launcher.Presentation.Shell.Navigation;
using Serilog;

namespace Launcher.Presentation.Shell;

/// <summary>
/// Shell 壳层 ViewModel。管理导航状态、认证状态和全局 UI 状态。
/// </summary>
public partial class ShellViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private static readonly ILogger Logger = Log.ForContext<ShellViewModel>();
    private readonly INavigationService _navigationService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;
    private readonly IEpicExchangeCodeLoginDialogService _epicLoginDialogService;
    private readonly IDownloadRuntimeStore _runtimeStore;
    private readonly IAppUpdateService _appUpdateService;
    private readonly INetworkMonitor _networkMonitor;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private static readonly HashSet<string> AuthRefreshRoutes =
    [
        NavigationRoute.FabLibrary,
        NavigationRoute.FabAssetDetail,
        NavigationRoute.EngineVersions,
        NavigationRoute.Plugins,
    ];

    // === 导航状态 ===
    [ObservableProperty] private string _currentRoute = string.Empty;
    [ObservableProperty] private bool _canGoBack;

    // === 认证状态 ===
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _displayName = "未登录";
    [ObservableProperty] private string _accountId = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private bool _isLoggingIn;
    [ObservableProperty] private bool _canContinueAuthorizationCodeLogin;

    /// <summary>未登录状态（方便 x:Bind 绑定取反）</summary>
    public bool IsNotAuthenticated => !IsAuthenticated;

    partial void OnIsAuthenticatedChanged(bool value) => OnPropertyChanged(nameof(IsNotAuthenticated));

    // === 全局状态 ===
    [ObservableProperty] private int _activeDownloadCount;
    [ObservableProperty] private bool _isNetworkAvailable = true;
    [ObservableProperty] private string _downloadSpeedText = string.Empty;
    [ObservableProperty] private bool _hasActiveDownloads;

    // === 更新状态 ===
    [ObservableProperty] private bool _hasPendingUpdate;
    [ObservableProperty] private string _pendingUpdateVersion = string.Empty;
    [ObservableProperty] private bool _pendingUpdateIsMandatory;
    [ObservableProperty] private bool _isDownloadingUpdate;
    [ObservableProperty] private double _updateDownloadProgress;

    /// <summary>非下载中状态（给按鈕的 IsEnabled 绑定）</summary>
    public bool IsNotDownloadingUpdate => !IsDownloadingUpdate;

    partial void OnIsDownloadingUpdateChanged(bool value)
        => OnPropertyChanged(nameof(IsNotDownloadingUpdate));

    /// <summary>是否可以跳过更新（非强制更新 = 可跳过）</summary>
    public bool CanSkipUpdate
        => !PendingUpdateIsMandatory;

    partial void OnPendingUpdateIsMandatoryChanged(bool value)
        => OnPropertyChanged(nameof(CanSkipUpdate));

    public ShellViewModel(
        INavigationService navigationService,
        IAuthService authService,
        IDialogService dialogService,
        IEpicExchangeCodeLoginDialogService epicLoginDialogService,
        IDownloadRuntimeStore runtimeStore,
        IAppUpdateService appUpdateService,
        INetworkMonitor networkMonitor)
    {
        _navigationService = navigationService;
        _authService = authService;
        _dialogService = dialogService;
        _epicLoginDialogService = epicLoginDialogService;
        _runtimeStore = runtimeStore;
        _appUpdateService = appUpdateService;
        _networkMonitor = networkMonitor;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // 同步初始网络状态
        _isNetworkAvailable = _networkMonitor.IsNetworkAvailable;

        // 监听会话过期事件
        _authService.SessionAuthenticated += OnSessionAuthenticated;
        _authService.SessionExpired += OnSessionExpired;

        // 监听下载进度，更新状态栏
        _runtimeStore.SnapshotChanged += OnDownloadSnapshotChanged;
        _runtimeStore.DownloadCompleted += OnDownloadCompleted;
        _runtimeStore.DownloadFailed += OnDownloadFailed;

        // 监听更新通知（仅依赖 Application 契约接口，不耦合 Background 层）
        _appUpdateService.UpdateAvailable += OnUpdateAvailable;

        // 监听网络状态变化
        _networkMonitor.NetworkStatusChanged += OnNetworkStatusChanged;

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
            var startResult = await _authService.StartExchangeCodeLoginAsync();
            if (!startResult.IsSuccess)
            {
                Logger.Warning("准备嵌入式登录失败 | Error={Error}", startResult.Error?.UserMessage);
                await _dialogService.ShowErrorAsync("准备登录失败", startResult.Error?.UserMessage ?? "无法准备嵌入式登录，请重试。");
                return;
            }

            var embeddedResult = await _epicLoginDialogService.ShowEpicExchangeCodeLoginAsync(startResult.Value!);
            if (embeddedResult.IsSuccess)
            {
                var completionResult = await _authService.CompleteLoginAsync(new AuthLoginCompletionInput
                {
                    Kind = AuthLoginCompletionKind.ExchangeCode,
                    Payload = embeddedResult.Value!,
                });

                if (completionResult.IsSuccess)
                {
                    CanContinueAuthorizationCodeLogin = false;
                    Logger.Information("嵌入式登录成功 | 用户={Name}", completionResult.Value!.DisplayName);
                    return;
                }

                Logger.Warning("嵌入式登录完成失败 | Error={Error} | Detail={Detail}", completionResult.Error?.UserMessage, completionResult.Error?.TechnicalMessage);
                await StartAuthorizationCodeFallbackAsync(
                    "嵌入式登录未完成，已切换到系统浏览器",
                    completionResult.Error?.UserMessage ?? "嵌入式登录未完成，已自动切换到系统浏览器登录。若浏览器没有自动回到应用，可使用下方高级入口继续。",
                    fallbackReason: "exchange_code_completion_failed");
                return;
            }

            if (string.Equals(embeddedResult.Error?.Code, "AUTH_WEBVIEW_LOGIN_CANCELLED", StringComparison.Ordinal))
            {
                Logger.Information("用户取消嵌入式登录");
                return;
            }

            Logger.Warning("嵌入式登录窗口失败 | Error={Error} | Detail={Detail}", embeddedResult.Error?.UserMessage, embeddedResult.Error?.TechnicalMessage);
            await StartAuthorizationCodeFallbackAsync(
                "嵌入式登录不可用，已切换到系统浏览器",
                embeddedResult.Error?.UserMessage ?? "嵌入式登录暂时不可用，已自动切换到系统浏览器登录。若浏览器没有自动回到应用，可使用下方高级入口继续。",
                fallbackReason: embeddedResult.Error?.Code ?? "embedded_login_unavailable");
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    [RelayCommand]
    private async Task ContinueAuthorizationCodeLoginAsync()
    {
        if (!CanContinueAuthorizationCodeLogin || IsLoggingIn) return;

        bool confirmed = await _dialogService.ShowConfirmAsync(
            "高级登录入口",
            "只有在浏览器没有自动回到应用，且你已经拿到 authorizationCode、完整回调链接，或浏览器最终只显示 Epic 返回的 JSON 响应时，才需要手动继续。是否继续？",
            "继续",
            "返回");

        if (!confirmed)
        {
            Logger.Information("用户取消进入高级登录入口");
            return;
        }

        IsLoggingIn = true;

        try
        {
            var input = await _dialogService.ShowTextInputAsync(
                "继续 Epic 登录",
                "请优先只粘贴 authorizationCode 或完整回调链接。若浏览器最终只显示 Epic 返回的 JSON 响应，也可以临时整段粘贴，应用只会提取其中的 authorizationCode 或 redirectUrl。",
                "粘贴 authorizationCode、回调链接，或必要时粘贴 JSON 响应",
                "完成登录",
                "取消");

            if (string.IsNullOrWhiteSpace(input))
            {
                Logger.Information("用户取消 authorization code/回调链接导入");
                return;
            }

            var result = await _authService.CompleteAuthorizationCodeLoginAsync(input);
            if (result.IsSuccess)
            {
                CanContinueAuthorizationCodeLogin = false;
                Logger.Information("登录成功 | 用户={Name}", result.Value!.DisplayName);
            }
            else
            {
                Logger.Warning("登录失败 | Error={Error} | Detail={Detail}", result.Error?.UserMessage, result.Error?.TechnicalMessage);
                await _dialogService.ShowErrorAsync("登录失败", result.Error?.UserMessage ?? "登录失败，请重试。");
            }
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    private async Task StartAuthorizationCodeFallbackAsync(string title, string message, string fallbackReason)
    {
        Logger.Information("切换到系统浏览器登录兜底 | Reason={Reason}", fallbackReason);

        var fallbackResult = await _authService.StartAuthorizationCodeLoginAsync();
        if (!fallbackResult.IsSuccess)
        {
            Logger.Warning("打开系统浏览器登录页失败 | Error={Error}", fallbackResult.Error?.UserMessage);
            await _dialogService.ShowErrorAsync("打开 Epic 登录页失败", fallbackResult.Error?.UserMessage ?? "无法打开 Epic 登录页面，请重试。");
            return;
        }

        CanContinueAuthorizationCodeLogin = true;
        await _dialogService.ShowInfoAsync(
            title,
            $"{message}\n\n若浏览器最终只显示 Epic 返回的 JSON 响应，请返回应用后使用“高级登录入口”。优先只粘贴 authorizationCode；若无法单独提取，也可临时整段粘贴，应用只会提取其中的 authorizationCode 或 redirectUrl。不要分享该响应中的其他敏感字段。");
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
        CanContinueAuthorizationCodeLogin = false;
        DisplayName = user.DisplayName;
        AccountId = user.AccountId;
        Email = user.Email;
    }

    private void ClearUserInfo()
    {
        IsAuthenticated = false;
        CanContinueAuthorizationCodeLogin = false;
        DisplayName = "未登录";
        AccountId = string.Empty;
        Email = string.Empty;
    }

    private void OnSessionExpired(SessionExpiredEvent evt)
    {
        _dispatcherQueue.TryEnqueue(() => ClearUserInfo());
        Logger.Warning("会话已过期 | 原因={Reason}", evt.Reason);
    }

    private void OnSessionAuthenticated(AuthUserInfo user)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            UpdateUserInfo(user);
            _ = RefreshAuthenticatedRouteAsync();
        });
    }

    private async Task RefreshAuthenticatedRouteAsync()
    {
        var route = _navigationService.CurrentRoute;
        if (!AuthRefreshRoutes.Contains(route))
        {
            return;
        }

        await _navigationService.ReloadCurrentAsync();
        UpdateNavigationState();
        Logger.Information("认证完成后刷新当前页面 | Route={Route}", route);
    }

    private void OnUpdateAvailable(UpdateAvailableEvent evt)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            PendingUpdateVersion = evt.Version;
            PendingUpdateIsMandatory = evt.IsMandatory;
            HasPendingUpdate = true;
            Logger.Information("UI 收到更新通知 | 版本={Version}", evt.Version);
        });
    }

    private void OnNetworkStatusChanged(bool isAvailable)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsNetworkAvailable = isAvailable;
            Logger.Information("网络状态更新 | IsAvailable={IsAvailable}", isAvailable);
        });
    }

    /// <summary>立即下载并安装可用更新</summary>
    [RelayCommand]
    private async Task DownloadAndApplyUpdateAsync()
    {
        if (!HasPendingUpdate || IsDownloadingUpdate) return;
        IsDownloadingUpdate = true;
        try
        {
            // 重新检查以获取完整的 UpdateInfo（运行时内存犴策策略）
            var checkResult = await _appUpdateService.CheckForUpdateAsync(CancellationToken.None);
            if (!checkResult.IsSuccess || checkResult.Value is null)
            {
                Logger.Warning("更新信息已无法获取，取消下载");
                IsDownloadingUpdate = false;
                return;
            }

            var progress = new Progress<double>(p =>
                _dispatcherQueue.TryEnqueue(() => UpdateDownloadProgress = p));

            var dlResult = await _appUpdateService.DownloadUpdateAsync(
                checkResult.Value, progress, CancellationToken.None);

            if (!dlResult.IsSuccess)
            {
                Logger.Warning("更新下载失败 | Error={Error}", dlResult.Error?.UserMessage);
                IsDownloadingUpdate = false;
                return;
            }

            // 下载完成后应用更新（无法取消，会退出应用）
            await _appUpdateService.ApplyUpdateAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "应用更新时发生异常");
            IsDownloadingUpdate = false;
        }
    }

    /// <summary>跳过当前待安装版本</summary>
    [RelayCommand]
    private async Task SkipCurrentUpdateAsync()
    {
        if (!HasPendingUpdate) return;
        await _appUpdateService.SkipVersionAsync(PendingUpdateVersion, CancellationToken.None);
        HasPendingUpdate = false;
        Logger.Information("用户跳过版本 | 版本={Version}", PendingUpdateVersion);
    }

    [RelayCommand]
    private Task NavigateToFabLibrary() => NavigateAsync(NavigationRoute.FabLibrary);

    [RelayCommand]
    private Task NavigateToDownloads() => NavigateAsync(NavigationRoute.Downloads);

    [RelayCommand]
    private Task NavigateToInstallations() => NavigateAsync(NavigationRoute.Installations);

    [RelayCommand]
    private Task NavigateToEngineVersions() => NavigateAsync(NavigationRoute.EngineVersions);

    [RelayCommand]
    private Task NavigateToPlugins() => NavigateAsync(NavigationRoute.Plugins);

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

    private void OnDownloadCompleted(DownloadCompletedEvent _) => RefreshDownloadStatus();
    private void OnDownloadFailed(DownloadFailedEvent _) => RefreshDownloadStatus();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _authService.SessionAuthenticated -= OnSessionAuthenticated;
        _authService.SessionExpired -= OnSessionExpired;
        _runtimeStore.SnapshotChanged -= OnDownloadSnapshotChanged;
        _runtimeStore.DownloadCompleted -= OnDownloadCompleted;
        _runtimeStore.DownloadFailed -= OnDownloadFailed;
        _appUpdateService.UpdateAvailable -= OnUpdateAvailable;
        _networkMonitor.NetworkStatusChanged -= OnNetworkStatusChanged;

        GC.SuppressFinalize(this);
    }
}
