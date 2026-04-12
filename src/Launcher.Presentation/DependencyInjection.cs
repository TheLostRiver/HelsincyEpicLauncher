// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Shell.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Launcher.Presentation;

/// <summary>
/// Presentation 层 DI 注册扩展
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        // 导航
        services.AddSingleton<INavigationService, StubNavigationService>();

        // ViewModel 和 View 注册将在后续任务中添加

        return services;
    }
}
