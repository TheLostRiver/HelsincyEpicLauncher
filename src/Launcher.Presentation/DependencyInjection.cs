// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Shell;
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
        // 导航（注册具体类型 + 接口，ShellPage 需要具体类型调用 SetFrame）
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

        // Toast 通知（注册具体类型 + 接口，ShellPage 需要具体类型调用 SetHost）
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());

        // Shell ViewModel
        services.AddSingleton<ShellViewModel>();

        return services;
    }
}
