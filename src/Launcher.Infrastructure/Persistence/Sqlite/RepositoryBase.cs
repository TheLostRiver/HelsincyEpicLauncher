// Copyright (c) Helsincy. All rights reserved.

using System.Data;
using Dapper;
using Launcher.Application.Persistence;
using Serilog;

namespace Launcher.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Repository 通用基类。提供基�?Dapper 的常�?CRUD 操作�?
/// 子类只需定义表名和业务语义方法�?
/// </summary>
/// <typeparam name="T">实体映射类型</typeparam>
internal abstract class RepositoryBase<T> where T : class
{
    private static readonly System.Text.RegularExpressions.Regex SafeTableNameRegex =
        new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly ILogger _logger;
    private readonly IDbConnectionFactory _connectionFactory;
    private string? _validatedTableName;

    /// <summary>数据库表名</summary>
    protected abstract string TableName { get; }

    /// <summary>经过验证的安全表名</summary>
    private string SafeTableName => _validatedTableName ??= ValidateTableName(TableName);

    protected RepositoryBase(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        _logger = Log.ForContext(GetType());
    }

    private static string ValidateTableName(string name)
    {
        if (string.IsNullOrEmpty(name) || !SafeTableNameRegex.IsMatch(name))
            throw new InvalidOperationException($"Invalid table name: '{name}'");
        return name;
    }

    /// <summary>
    /// 创建并返回一个已打开的数据库连接。调用方负责 Dispose�?
    /// </summary>
    protected async Task<IDbConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        return await _connectionFactory.CreateConnectionAsync(ct);
    }

    /// <summary>
    /// 根据主键查询单条记录�?
    /// </summary>
    protected async Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync(ct);
        var result = await connection.QueryFirstOrDefaultAsync<T>(
            $"SELECT * FROM {SafeTableName} WHERE id = @Id",
            new { Id = id });

        _logger.Debug("查询 {Table} | Id={Id} | 找到={Found}", TableName, id, result is not null);
        return result;
    }

    /// <summary>
    /// 查询所有记录�?
    /// </summary>
    protected async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync(ct);
        var result = (await connection.QueryAsync<T>(
            $"SELECT * FROM {SafeTableName}")).ToList();

        _logger.Debug("查询所�?{Table} | 总计={Count}", TableName, result.Count);
        return result;
    }

    /// <summary>
    /// 执行带参数的查询，返回匹配记录列表�?
    /// </summary>
    protected async Task<IReadOnlyList<T>> QueryAsync(string sql, object? param = null, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync(ct);
        var result = (await connection.QueryAsync<T>(sql, param)).ToList();

        _logger.Debug("查询 {Table} | SQL={Sql} | 结果�?{Count}", TableName, sql, result.Count);
        return result;
    }

    /// <summary>
    /// 插入一条记录�?
    /// </summary>
    protected async Task<int> InsertAsync(string sql, object param, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync(ct);
        var affected = await connection.ExecuteAsync(sql, param);

        _logger.Debug("插入 {Table} | 影响行数={Affected}", TableName, affected);
        return affected;
    }

    /// <summary>
    /// 更新记录�?
    /// </summary>
    protected async Task<int> UpdateAsync(string sql, object param, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync(ct);
        var affected = await connection.ExecuteAsync(sql, param);

        _logger.Debug("更新 {Table} | 影响行数={Affected}", TableName, affected);
        return affected;
    }

    /// <summary>
    /// 根据主键删除单条记录�?
    /// </summary>
    protected async Task<bool> DeleteByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync(ct);
        var affected = await connection.ExecuteAsync(
            $"DELETE FROM {SafeTableName} WHERE id = @Id",
            new { Id = id });

        _logger.Debug("删除 {Table} | Id={Id} | 已删�?{Deleted}", TableName, id, affected > 0);
        return affected > 0;
    }

    /// <summary>
    /// 查询记录总数�?
    /// </summary>
    protected async Task<int> CountAsync(CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {SafeTableName}");
    }

    /// <summary>
    /// 在事务中执行多条语句�?
    /// </summary>
    protected async Task ExecuteInTransactionAsync(Func<IDbConnection, IDbTransaction, Task> action, CancellationToken ct = default)
    {
        using var connection = await OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        try
        {
            await action(connection, transaction);
            transaction.Commit();
            _logger.Debug("事务提交成功 | {Table}", TableName);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.Error(ex, "事务回滚 | {Table}", TableName);
            throw;
        }
    }
}
