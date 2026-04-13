// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Infrastructure.Persistence.Sqlite.Migrations;

/// <summary>
/// 迁移 004：创建键值设置表。存储动态运行时键值配置（区别于 user.settings.json 的结构化配置）。
/// </summary>
internal sealed class Migration_004_SettingsKv : IMigration
{
    public int Version => 4;
    public string Description => "创建键值设置表";
    public string Sql => """
        CREATE TABLE IF NOT EXISTS settings_kv (
            key         TEXT    PRIMARY KEY,
            value       TEXT    NOT NULL,
            category    TEXT    NOT NULL DEFAULT 'General',
            updated_at  TEXT    NOT NULL DEFAULT (datetime('now'))
        );

        CREATE INDEX IF NOT EXISTS idx_settings_kv_category ON settings_kv(category);
        """;
}
