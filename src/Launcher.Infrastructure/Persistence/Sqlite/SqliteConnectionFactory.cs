// Copyright (c) Helsincy. All rights reserved.

using System.Data;
using Launcher.Application.Persistence;
using Launcher.Shared.Configuration;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Launcher.Infrastructure.Persistence.Sqlite;

/// <summary>
/// SQLite 连接工厂。管理数据库文件路径和 WAL 模式配置。
/// </summary>
internal sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private static readonly ILogger Logger = Log.ForContext<SqliteConnectionFactory>();
    private readonly string _connectionString;

    public SqliteConnectionFactory(IAppConfigProvider configProvider)
    {
        var dbPath = Path.Combine(configProvider.DataPath, "launcher.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        Logger.Debug("SQLite 数据库路径: {DbPath}", dbPath);
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        // 启用 WAL 模式（提升并发读写性能）
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        await cmd.ExecuteNonQueryAsync(ct);

        return connection;
    }
}
