// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.Diagnostics.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Application.Persistence;
using Launcher.Infrastructure.Auth;
using Launcher.Infrastructure.Configuration;
using Launcher.Infrastructure.Diagnostics;
using Launcher.Infrastructure.Persistence.Sqlite;
using Launcher.Infrastructure.Persistence.Sqlite.Migrations;
using Launcher.Infrastructure.Settings;
using Launcher.Shared.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Launcher.Infrastructure;

/// <summary>
/// Infrastructure 层 DI 注册扩展
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // 配置
        services.AddSingleton<IAppConfigProvider, AppConfigProvider>();

        // 用户设置（注册具体类型 + 双接口）
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsCommandService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<ISettingsReadService>(sp => sp.GetRequiredService<SettingsService>());

        // 数据库
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IMigration, Migration_001_AppSettings>();
        services.AddSingleton<IMigration, Migration_002_Downloads>();
        services.AddSingleton<IMigration, Migration_003_Installations>();
        services.AddSingleton<IMigration, Migration_004_SettingsKv>();
        services.AddSingleton<IDatabaseInitializer, MigrationRunner>();

        // 诊断
        services.AddSingleton<IDiagnosticsReadService, DiagnosticsService>();
        services.AddSingleton<ICacheManager, CacheManager>();

        // 认证
        services.AddHttpClient("EpicAuth");
        services.AddSingleton<ITokenStore, FileTokenStore>();
        services.AddSingleton<EpicOAuthHandler>();
        services.AddSingleton<IAuthService, AuthService>();

        return services;
    }
}
