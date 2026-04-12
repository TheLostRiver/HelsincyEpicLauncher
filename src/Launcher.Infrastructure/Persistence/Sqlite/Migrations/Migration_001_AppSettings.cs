// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Infrastructure.Persistence.Sqlite.Migrations;

/// <summary>
/// 初始迁移：创建应用配置表（用于存储运行时键值配置）。
/// </summary>
internal sealed class Migration_001_AppSettings : IMigration
{
    public int Version => 1;
    public string Description => "创建应用配置表";
    public string Sql => """
        CREATE TABLE IF NOT EXISTS app_settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """;
}
