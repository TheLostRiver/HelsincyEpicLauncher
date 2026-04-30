// Copyright (c) Helsincy. All rights reserved.

using Launcher.Background.Auth;
using Launcher.Background.Hosting;
using Launcher.Background.Installations;
using Launcher.Background.Network;
using Launcher.Background.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace Launcher.Background;

/// <summary>
/// Background 层 DI 注册扩展
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBackground(this IServiceCollection services)
    {
        // Token 自动刷新
        services.AddSingleton<TokenRefreshBackgroundService>();
        services.AddSingleton<IBackgroundWorker>(sp => sp.GetRequiredService<TokenRefreshBackgroundService>());

        // 下载完成后自动安装
        services.AddSingleton<AutoInstallWorker>();
        services.AddSingleton<IBackgroundWorker>(sp => sp.GetRequiredService<AutoInstallWorker>());

        // 自动更新检查
        services.AddSingleton<AppUpdateWorker>();
        services.AddSingleton<IBackgroundWorker>(sp => sp.GetRequiredService<AppUpdateWorker>());

        // 网络监视
        services.AddSingleton<NetworkMonitorWorker>();
        services.AddSingleton<IBackgroundWorker>(sp => sp.GetRequiredService<NetworkMonitorWorker>());

        // 统一后台任务宿主
        services.AddSingleton<IBackgroundTaskHost, BackgroundTaskHost>();

        return services;
    }
}
