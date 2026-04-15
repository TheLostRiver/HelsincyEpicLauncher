// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Infrastructure.Persistence.Sqlite.Migrations;

/// <summary>
/// 迁移 006：补充缺失的查询索引，优化下载模块频繁查询路径。
/// </summary>
internal sealed class Migration_006_Indexes : IMigration
{
    public int Version => 6;
    public string Description => "补充查询索引（downloads.asset_id、chunk_checkpoints.task_id）";
    public string Sql => """
        CREATE INDEX IF NOT EXISTS idx_downloads_asset_id
            ON downloads(asset_id);

        CREATE INDEX IF NOT EXISTS idx_chunk_checkpoints_task_id
            ON chunk_checkpoints(task_id);
        """;
}
