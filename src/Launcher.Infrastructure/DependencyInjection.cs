// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Persistence;
using Launcher.Infrastructure.Configuration;
using Launcher.Infrastructure.Persistence.Sqlite;
using Launcher.Infrastructure.Persistence.Sqlite.Migrations;
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

        // 数据库
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IMigration, Migration_001_AppSettings>();
        services.AddSingleton<IDatabaseInitializer, MigrationRunner>();

        return services;
    }
}
