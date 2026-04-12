// Copyright (c) Helsincy. All rights reserved.

using Microsoft.UI.Xaml.Controls;
using Launcher.Presentation.Modules.Diagnostics;
using Launcher.Presentation.Modules.Downloads;
using Launcher.Presentation.Modules.EngineVersions;
using Launcher.Presentation.Modules.FabLibrary;
using Launcher.Presentation.Modules.Settings;
using Serilog;

namespace Launcher.Presentation.Shell.Navigation;

/// <summary>
/// 导航服务完整实现。管理 WinUI 3 Frame 页面导航、路由映射和导航历史。
/// </summary>
public sealed class NavigationService : INavigationService
{
    private static readonly ILogger Logger = Log.ForContext<NavigationService>();

    private Frame? _frame;
    private string _currentRoute = string.Empty;
    private readonly Stack<string> _history = new();

    /// <summary>
    /// 路由 → Page 类型映射表
    /// </summary>
    private static readonly Dictionary<string, Type> RouteMap = new()
    {
        [NavigationRoute.FabLibrary] = typeof(FabLibraryPage),
        [NavigationRoute.Downloads] = typeof(DownloadsPage),
        [NavigationRoute.EngineVersions] = typeof(EngineVersionsPage),
        [NavigationRoute.Settings] = typeof(SettingsPage),
        [NavigationRoute.Diagnostics] = typeof(DiagnosticsPage),
    };

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public string CurrentRoute => _currentRoute;

    /// <summary>
    /// 设置导航宿主 Frame。由 ShellPage 在加载时调用。
    /// </summary>
    public void SetFrame(Frame frame)
    {
        _frame = frame;
        Logger.Debug("NavigationService Frame 已设置");
    }

    public Task NavigateAsync(string route, object? parameter = null)
    {
        if (_frame is null)
        {
            Logger.Warning("导航失败：Frame 未设置");
            return Task.CompletedTask;
        }

        if (!RouteMap.TryGetValue(route, out var pageType))
        {
            Logger.Warning("导航失败：未知路由 {Route}", route);
            return Task.CompletedTask;
        }

        // 跳过重复导航
        if (route == _currentRoute)
        {
            return Task.CompletedTask;
        }

        _frame.Navigate(pageType, parameter);

        if (!string.IsNullOrEmpty(_currentRoute))
        {
            _history.Push(_currentRoute);
        }
        _currentRoute = route;

        Logger.Information("导航到 {Route}", route);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        if (_frame?.CanGoBack != true)
        {
            Logger.Debug("导航历史为空，无法返回");
            return Task.CompletedTask;
        }

        _frame.GoBack();

        if (_history.TryPop(out var previous))
        {
            Logger.Information("返回到 {Route}（从 {CurrentRoute}）", previous, _currentRoute);
            _currentRoute = previous;
        }

        return Task.CompletedTask;
    }
}
