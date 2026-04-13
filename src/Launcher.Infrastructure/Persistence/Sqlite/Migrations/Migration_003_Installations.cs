// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Infrastructure.Persistence.Sqlite.Migrations;

/// <summary>
/// 迁移 003：创建已安装资产表。跟踪已安装的 Fab 资产和引擎版本。
/// </summary>
internal sealed class Migration_003_Installations : IMigration
{
    public int Version => 3;
    public string Description => "创建已安装资产表";
    public string Sql => """
        CREATE TABLE IF NOT EXISTS installations (
            id              TEXT    PRIMARY KEY,
            asset_id        TEXT    NOT NULL,
            asset_name      TEXT    NOT NULL,
            version         TEXT    NOT NULL,
            install_path    TEXT    NOT NULL,
            size_bytes      INTEGER NOT NULL DEFAULT 0,
            asset_type      TEXT    NOT NULL DEFAULT 'FabAsset',
            installed_at    TEXT    NOT NULL DEFAULT (datetime('now')),
            updated_at      TEXT    NOT NULL DEFAULT (datetime('now'))
        );

        CREATE INDEX IF NOT EXISTS idx_installations_asset_id ON installations(asset_id);
        CREATE INDEX IF NOT EXISTS idx_installations_asset_type ON installations(asset_type);
        """;
}
