// Copyright (c) Helsincy. All rights reserved.

using System.Globalization;
using System.Text.Json;
using Dapper;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Application.Persistence;
using Launcher.Domain.Installations;
using Serilog;

namespace Launcher.Infrastructure.Installations;

/// <summary>
/// 安装记录仓储实现（SQLite + Dapper）。包含 Manifest JSON 文件持久化。
/// </summary>
internal sealed class InstallationRepository : IInstallationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger _logger = Log.ForContext<InstallationRepository>();

    public InstallationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Installation?> GetByIdAsync(string id, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<InstallRow>(
            "SELECT * FROM installations WHERE id = @Id", new { Id = id });
        return row is null ? null : MapToDomain(row);
    }

    public async Task<Installation?> GetByAssetIdAsync(string assetId, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<InstallRow>(
            "SELECT * FROM installations WHERE asset_id = @AssetId", new { AssetId = assetId });
        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<Installation>> GetAllAsync(CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var rows = await conn.QueryAsync<InstallRow>(
            "SELECT * FROM installations ORDER BY installed_at DESC");
        return rows.Select(MapToDomain).ToList();
    }

    public async Task InsertAsync(Installation entity, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO installations (id, asset_id, asset_name, version, install_path, size_bytes, asset_type, installed_at, updated_at)
            VALUES (@Id, @AssetId, @AssetName, @Version, @InstallPath, @SizeBytes, @AssetType, @InstalledAt, @UpdatedAt)
            """, MapToRow(entity));
        _logger.Debug("插入安装记录 {AssetId}", entity.AssetId);
    }

    public async Task UpdateAsync(Installation entity, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE installations
            SET asset_name = @AssetName, version = @Version, install_path = @InstallPath,
                size_bytes = @SizeBytes, asset_type = @AssetType, updated_at = @UpdatedAt
            WHERE id = @Id
            """, MapToRow(entity));
        _logger.Debug("更新安装记录 {AssetId}", entity.AssetId);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        await conn.ExecuteAsync("DELETE FROM installations WHERE id = @Id", new { Id = id });
        _logger.Debug("删除安装记录 {Id}", id);
    }

    // ===== Manifest 持久化 =====

    public Task SaveManifestAsync(string assetId, InstallManifest manifest, CancellationToken ct)
    {
        var path = GetManifestPath(assetId);
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.InstallManifest);
        File.WriteAllText(path, json);
        _logger.Debug("保存 Manifest {AssetId} → {Path}", assetId, path);
        return Task.CompletedTask;
    }

    public Task<InstallManifest?> GetManifestAsync(string assetId, CancellationToken ct)
    {
        var path = GetManifestPath(assetId);
        if (!File.Exists(path))
            return Task.FromResult<InstallManifest?>(null);

        var json = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize(json, ManifestJsonContext.Default.InstallManifest);
        return Task.FromResult(manifest);
    }

    public Task DeleteManifestAsync(string assetId, CancellationToken ct)
    {
        var path = GetManifestPath(assetId);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.Debug("删除 Manifest {AssetId}", assetId);
        }
        return Task.CompletedTask;
    }

    private static string GetManifestPath(string assetId)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "HelsincyEpicLauncher", "manifests", $"{assetId}.json");
    }

    // ===== Mapping =====

    private static Installation MapToDomain(InstallRow row)
    {
        // 已安装的记录默认恢复为 Installed 状态
        return new Installation(
            row.id,
            row.asset_id,
            row.asset_name,
            row.version,
            row.install_path,
            row.size_bytes,
            row.asset_type,
            InstallState.Installed,
            DateTimeOffset.Parse(row.installed_at, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(row.updated_at, CultureInfo.InvariantCulture));
    }

    private static object MapToRow(Installation entity) => new
    {
        Id = entity.Id,
        AssetId = entity.AssetId,
        AssetName = entity.AssetName,
        Version = entity.Version,
        InstallPath = entity.InstallPath,
        SizeBytes = entity.SizeBytes,
        AssetType = entity.AssetType,
        InstalledAt = entity.InstalledAt.ToString("o"),
        UpdatedAt = entity.UpdatedAt.ToString("o"),
    };

    internal sealed class InstallRow
    {
        public string id { get; set; } = default!;
        public string asset_id { get; set; } = default!;
        public string asset_name { get; set; } = default!;
        public string version { get; set; } = default!;
        public string install_path { get; set; } = default!;
        public long size_bytes { get; set; }
        public string asset_type { get; set; } = default!;
        public string installed_at { get; set; } = default!;
        public string updated_at { get; set; } = default!;
    }
}

/// <summary>
/// Manifest JSON 序列化上下文（AOT 安全）
/// </summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(InstallManifest))]
internal sealed partial class ManifestJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
