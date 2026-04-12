// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Shell.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace Launcher.Presentation.Shell;

/// <summary>
/// Shell 壳层页面。包含左侧 NavigationView 导航栏和右侧内容 Frame。
/// </summary>
public sealed partial class ShellPage : UserControl
{
    private static readonly ILogger Logger = Log.ForContext<ShellPage>();
    private readonly NavigationService _navigationService;
    private readonly DialogService _dialogService;
    private readonly ThemeService _themeService;

    public ShellViewModel ViewModel { get; }

    public ShellPage(ShellViewModel viewModel, NavigationService navigationService, NotificationService notificationService, DialogService dialogService, ThemeService themeService)
    {
        this.InitializeComponent();

        ViewModel = viewModel;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _themeService = themeService;

        // 将 ContentFrame 设置为导航宿主
        _navigationService.SetFrame(ContentFrame);

        // 设置 Toast 通知宿主面板
        notificationService.SetHost(ToastHost);

        Logger.Debug("ShellPage 已创建");
    }

    /// <summary>
    /// 页面加载完成后，导航到默认页面（Fab 资产库）
    /// </summary>
    private void ShellPage_Loaded(object sender, RoutedEventArgs e)
    {
        // 设置 DialogService 的 XamlRoot（Loaded 后 XamlRoot 才有效）
        _dialogService.SetXamlRoot(this.XamlRoot);

        // 初始化主题服务（加载保存的主题并应用）
        _themeService.Initialize(this);

        // 选中第一个导航项，触发 SelectionChanged → 导航到默认页面
        NavView.SelectedItem = NavView.MenuItems[0];
        Logger.Information("ShellPage 已加载，默认导航到 Fab 资产库");
    }

    /// <summary>
    /// NavigationView 选项变更时，执行页面导航
    /// </summary>
    private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string route)
        {
            await _navigationService.NavigateAsync(route);
            ViewModel.CurrentRoute = _navigationService.CurrentRoute;
            ViewModel.CanGoBack = _navigationService.CanGoBack;
        }
    }
}
