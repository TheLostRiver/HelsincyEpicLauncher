// Copyright (c) Helsincy. All rights reserved.

using Launcher.Background.Auth;
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

        // 下载完成后自动安装
        services.AddSingleton<AutoInstallWorker>();

        // 自动更新检查
        services.AddSingleton<AppUpdateWorker>();

        // 网络监视
        services.AddSingleton<NetworkMonitorWorker>();

        return services;
    }
}
