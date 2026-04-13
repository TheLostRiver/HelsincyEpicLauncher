// Copyright (c) Helsincy. All rights reserved.

using System.Data;
using Dapper;
using FluentAssertions;
using Launcher.Application.Persistence;
using Launcher.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;

namespace Launcher.Tests.Integration.Persistence;

/// <summary>
/// RepositoryBase CRUD 集成测试。使用内存 SQLite 数据库。
/// </summary>
public sealed class RepositoryBaseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestConnectionFactory _connectionFactory;
    private readonly TestRepository _repository;

    public RepositoryBaseTests()
    {
        // 使用共享内存模式，确保多次打开返回同一数据库
        _connection = new SqliteConnection("Data Source=RepoTest;Mode=Memory;Cache=Shared");
        _connection.Open();

        // 创建测试表
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS test_items (
                id      TEXT PRIMARY KEY,
                name    TEXT NOT NULL,
                value   INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        _connectionFactory = new TestConnectionFactory();
        _repository = new TestRepository(_connectionFactory);
    }

    [Fact]
    public async Task InsertAndGetById_ShouldReturnInsertedRecord()
    {
        var id = Guid.NewGuid().ToString();
        await _repository.AddItemAsync(id, "测试项目", 42);

        var result = await _repository.GetItemByIdAsync(id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("测试项目");
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task GetAll_ShouldReturnAllRecords()
    {
        await _repository.AddItemAsync(Guid.NewGuid().ToString(), "A", 1);
        await _repository.AddItemAsync(Guid.NewGuid().ToString(), "B", 2);

        var results = await _repository.GetAllItemsAsync();

        results.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task DeleteById_ShouldRemoveRecord()
    {
        var id = Guid.NewGuid().ToString();
        await _repository.AddItemAsync(id, "临时", 99);

        var deleted = await _repository.DeleteItemAsync(id);

        deleted.Should().BeTrue();
        var result = await _repository.GetItemByIdAsync(id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteById_NonExistent_ShouldReturnFalse()
    {
        var deleted = await _repository.DeleteItemAsync("nonexistent-id");

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task Count_ShouldReturnCorrectNumber()
    {
        // 清空表以获得准确计数
        using var conn = new SqliteConnection("Data Source=RepoTest;Mode=Memory;Cache=Shared");
        await conn.OpenAsync();
        await conn.ExecuteAsync("DELETE FROM test_items");

        await _repository.AddItemAsync(Guid.NewGuid().ToString(), "X", 10);
        await _repository.AddItemAsync(Guid.NewGuid().ToString(), "Y", 20);

        var count = await _repository.GetCountAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task Update_ShouldModifyRecord()
    {
        var id = Guid.NewGuid().ToString();
        await _repository.AddItemAsync(id, "原始名称", 100);

        await _repository.UpdateItemAsync(id, "新名称", 200);

        var result = await _repository.GetItemByIdAsync(id);
        result.Should().NotBeNull();
        result!.Name.Should().Be("新名称");
        result.Value.Should().Be(200);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    // === 测试辅助类 ===

    /// <summary>
    /// 内存 SQLite 连接工厂（共享缓存模式保证多连接访问同一数据库）
    /// </summary>
    private sealed class TestConnectionFactory : IDbConnectionFactory
    {
        public async Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default)
        {
            var connection = new SqliteConnection("Data Source=RepoTest;Mode=Memory;Cache=Shared");
            await connection.OpenAsync(ct);
            return connection;
        }
    }

    /// <summary>
    /// 测试用具体 Repository 实现
    /// </summary>
    private sealed class TestRepository : RepositoryBase<TestItem>
    {
        protected override string TableName => "test_items";

        public TestRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public Task<TestItem?> GetItemByIdAsync(string id, CancellationToken ct = default)
            => GetByIdAsync(id, ct);

        public Task<IReadOnlyList<TestItem>> GetAllItemsAsync(CancellationToken ct = default)
            => GetAllAsync(ct);

        public Task<int> AddItemAsync(string id, string name, int value, CancellationToken ct = default)
            => InsertAsync(
                "INSERT INTO test_items (id, name, value) VALUES (@Id, @Name, @Value)",
                new { Id = id, Name = name, Value = value },
                ct);

        public Task<int> UpdateItemAsync(string id, string name, int value, CancellationToken ct = default)
            => UpdateAsync(
                "UPDATE test_items SET name = @Name, value = @Value WHERE id = @Id",
                new { Id = id, Name = name, Value = value },
                ct);

        public Task<bool> DeleteItemAsync(string id, CancellationToken ct = default)
            => DeleteByIdAsync(id, ct);

        public Task<int> GetCountAsync(CancellationToken ct = default)
            => CountAsync(ct);
    }

    /// <summary>
    /// 测试用数据模型
    /// </summary>
    private sealed class TestItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
