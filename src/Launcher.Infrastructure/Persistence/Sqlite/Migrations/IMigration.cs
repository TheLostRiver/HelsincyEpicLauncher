// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Infrastructure.Persistence.Sqlite.Migrations;

/// <summary>
/// 数据库迁移接口。每个 Migration 实现一个版本升级步骤。
/// </summary>
internal interface IMigration
{
    /// <summary>迁移版本号（严格递增）</summary>
    int Version { get; }

    /// <summary>迁移描述</summary>
    string Description { get; }

    /// <summary>SQL 语句</summary>
    string Sql { get; }
}
