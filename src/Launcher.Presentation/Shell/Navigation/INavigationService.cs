// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Presentation.Shell.Navigation;

/// <summary>
/// 页面导航服务。Shell 壳层和各模块通过此接口进行页面跳转。
/// </summary>
public interface INavigationService
{
    /// <summary>导航到指定路由</summary>
    Task NavigateAsync(string route, object? parameter = null);

    /// <summary>返回上一页</summary>
    Task GoBackAsync();

    /// <summary>是否可以返回</summary>
    bool CanGoBack { get; }

    /// <summary>当前路由</summary>
    string CurrentRoute { get; }
}
