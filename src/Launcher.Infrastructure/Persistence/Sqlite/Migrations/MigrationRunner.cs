// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Persistence;
using Serilog;

namespace Launcher.Infrastructure.Persistence.Sqlite.Migrations;

/// <summary>
/// 数据库迁移执行器。按版本号顺序执行未应用的迁移脚本。
/// 使用 __migration_history 表跟踪已执行的版本。
/// </summary>
internal sealed class MigrationRunner : IDatabaseInitializer
{
    private static readonly ILogger Logger = Log.ForContext<MigrationRunner>();
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IReadOnlyList<IMigration> _migrations;

    public MigrationRunner(IDbConnectionFactory connectionFactory, IEnumerable<IMigration> migrations)
    {
        _connectionFactory = connectionFactory;
        _migrations = migrations.OrderBy(m => m.Version).ToList();
    }

    /// <summary>
    /// 执行所有待应用的迁移。在事务中逐个执行。
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)
            await _connectionFactory.CreateConnectionAsync(ct);

        // 确保迁移历史表存在
        await EnsureHistoryTableAsync(connection, ct);

        var appliedVersions = await GetAppliedVersionsAsync(connection, ct);

        foreach (var migration in _migrations)
        {
            if (appliedVersions.Contains(migration.Version))
                continue;

            Logger.Information("执行数据库迁移 v{Version}: {Description}",
                migration.Version, migration.Description);

            await using var transaction = connection.BeginTransaction();
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = migration.Sql;
                await cmd.ExecuteNonQueryAsync(ct);

                // 记录迁移历史
                await using var historyCmd = connection.CreateCommand();
                historyCmd.Transaction = transaction;
                historyCmd.CommandText =
                    "INSERT INTO __migration_history (version, description, applied_at) VALUES ($v, $d, datetime('now'));";
                historyCmd.Parameters.AddWithValue("$v", migration.Version);
                historyCmd.Parameters.AddWithValue("$d", migration.Description);
                await historyCmd.ExecuteNonQueryAsync(ct);

                transaction.Commit();
                Logger.Information("迁移 v{Version} 执行成功", migration.Version);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Logger.Error(ex, "迁移 v{Version} 执行失败", migration.Version);
                throw;
            }
        }
    }

    private static async Task EnsureHistoryTableAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS __migration_history (
                version     INTEGER PRIMARY KEY,
                description TEXT    NOT NULL,
                applied_at  TEXT    NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<HashSet<int>> GetAppliedVersionsAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection, CancellationToken ct)
    {
        var versions = new HashSet<int>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version FROM __migration_history;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            versions.Add(reader.GetInt32(0));
        }
        return versions;
    }
}
