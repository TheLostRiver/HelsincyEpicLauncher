// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Modules.Diagnostics;
using Launcher.Presentation.Modules.Settings;
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

        // 对话框（注册具体类型 + 接口，ShellPage 需要具体类型调用 SetXamlRoot）
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());

        // 主题切换
        services.AddSingleton<ThemeService>();

        // Shell ViewModel
        services.AddSingleton<ShellViewModel>();

        // Settings ViewModel（Transient：每次导航到设置页面刷新最新配置）
        services.AddTransient<SettingsViewModel>();

        // Diagnostics ViewModel（Transient：每次导航刷新）
        services.AddTransient<DiagnosticsViewModel>();

        return services;
    }
}
