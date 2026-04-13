// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Infrastructure.Persistence.Sqlite.Migrations;

/// <summary>
/// 迁移 002：创建下载任务表。存储下载元数据、状态和断点续传信息。
/// </summary>
internal sealed class Migration_002_Downloads : IMigration
{
    public int Version => 2;
    public string Description => "创建下载任务表";
    public string Sql => """
        CREATE TABLE IF NOT EXISTS downloads (
            id              TEXT    PRIMARY KEY,
            asset_id        TEXT    NOT NULL,
            asset_name      TEXT    NOT NULL,
            download_url    TEXT    NOT NULL,
            save_path       TEXT    NOT NULL,
            total_bytes     INTEGER NOT NULL DEFAULT 0,
            downloaded_bytes INTEGER NOT NULL DEFAULT 0,
            status          TEXT    NOT NULL DEFAULT 'Pending',
            chunk_count     INTEGER NOT NULL DEFAULT 1,
            retry_count     INTEGER NOT NULL DEFAULT 0,
            error_message   TEXT,
            created_at      TEXT    NOT NULL DEFAULT (datetime('now')),
            updated_at      TEXT    NOT NULL DEFAULT (datetime('now')),
            completed_at    TEXT
        );

        CREATE INDEX IF NOT EXISTS idx_downloads_status ON downloads(status);
        CREATE INDEX IF NOT EXISTS idx_downloads_created_at ON downloads(created_at);
        """;
}
