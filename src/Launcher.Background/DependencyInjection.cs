// Copyright (c) Helsincy. All rights reserved.

using Launcher.Background.Auth;
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

        return services;
    }
}
