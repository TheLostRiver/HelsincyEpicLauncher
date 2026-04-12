// Copyright (c) Helsincy. All rights reserved.

using Serilog;

namespace Launcher.Presentation.Shell.Navigation;

/// <summary>
/// 导航服务桩实现。仅记录导航请求日志，后续替换为真实 WinUI 导航实现。
/// </summary>
internal sealed class StubNavigationService : INavigationService
{
    private static readonly ILogger Logger = Log.ForContext<StubNavigationService>();
    private string _currentRoute = string.Empty;
    private readonly Stack<string> _history = new();

    public bool CanGoBack => _history.Count > 0;

    public string CurrentRoute => _currentRoute;

    public Task NavigateAsync(string route, object? parameter = null)
    {
        if (!string.IsNullOrEmpty(_currentRoute))
        {
            _history.Push(_currentRoute);
        }

        _currentRoute = route;
        Logger.Information("导航到 {Route}（参数: {Parameter}）", route, parameter);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        if (_history.Count == 0)
        {
            Logger.Warning("导航历史为空，无法返回");
            return Task.CompletedTask;
        }

        var previous = _history.Pop();
        Logger.Information("返回到 {Route}（从 {CurrentRoute}）", previous, _currentRoute);
        _currentRoute = previous;
        return Task.CompletedTask;
    }
}
