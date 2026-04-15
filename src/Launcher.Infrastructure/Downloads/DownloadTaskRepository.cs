// Copyright (c) Helsincy. All rights reserved.

using System.Text.Json;
using Dapper;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Persistence;
using Launcher.Domain.Downloads;
using Launcher.Infrastructure.Persistence.Sqlite;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 下载任务仓储实现（SQLite + Dapper）。包含 Checkpoint 持久化。
/// </summary>
internal sealed class DownloadTaskRepository : IDownloadTaskRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger _logger = Log.ForContext<DownloadTaskRepository>();

    public DownloadTaskRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DownloadTask?> GetByIdAsync(DownloadTaskId id, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<DownloadRow>(
            "SELECT * FROM downloads WHERE id = @Id",
            new { Id = id.Value.ToString() });
        return row is null ? null : MapToDomain(row);
    }

    public async Task<DownloadTask?> GetByAssetIdAsync(string assetId, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<DownloadRow>(
            "SELECT * FROM downloads WHERE asset_id = @AssetId ORDER BY created_at DESC LIMIT 1",
            new { AssetId = assetId });
        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<DownloadTask>> GetActiveTasksAsync(CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var rows = await conn.QueryAsync<DownloadRow>(
            "SELECT * FROM downloads WHERE status NOT IN ('Completed', 'Cancelled') ORDER BY created_at");
        return rows.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<DownloadTaskId>> GetTaskIdsByStateAsync(DownloadState state, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var ids = await conn.QueryAsync<string>(
            "SELECT id FROM downloads WHERE status = @Status",
            new { Status = state.ToString() });
        return ids.Select(id => new DownloadTaskId(Guid.Parse(id))).ToList();
    }

    public async Task<IReadOnlyList<DownloadTaskId>> GetTaskIdsExcludingStatesAsync(IReadOnlyList<DownloadState> excludedStates, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var excluded = excludedStates.Select(s => s.ToString()).ToList();
        var ids = await conn.QueryAsync<string>(
            "SELECT id FROM downloads WHERE status NOT IN @ExcludedStates",
            new { ExcludedStates = excluded });
        return ids.Select(id => new DownloadTaskId(Guid.Parse(id))).ToList();
    }

    public async Task<IReadOnlyList<DownloadTask>> GetHistoryAsync(int limit, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var rows = await conn.QueryAsync<DownloadRow>(
            "SELECT * FROM downloads WHERE status IN ('Completed', 'Cancelled', 'Failed') ORDER BY updated_at DESC LIMIT @Limit",
            new { Limit = limit });
        return rows.Select(MapToDomain).ToList();
    }

    public async Task InsertAsync(DownloadTask task, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO downloads (id, asset_id, asset_name, download_url, save_path, total_bytes, downloaded_bytes, status, retry_count, error_message, created_at, updated_at)
            VALUES (@Id, @AssetId, @AssetName, @DownloadUrl, @SavePath, @TotalBytes, @DownloadedBytes, @Status, @RetryCount, @ErrorMessage, @CreatedAt, @UpdatedAt)
            """,
            MapToRow(task));
        _logger.Debug("插入下载任务 {TaskId}", task.Id);
    }

    public async Task UpdateAsync(DownloadTask task, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE downloads
            SET downloaded_bytes = @DownloadedBytes, status = @Status, retry_count = @RetryCount,
                error_message = @ErrorMessage, updated_at = @UpdatedAt, total_bytes = @TotalBytes
            WHERE id = @Id
            """,
            MapToRow(task));
        _logger.Debug("更新下载任务 {TaskId}, 状态={Status}", task.Id, task.State);
    }

    // ===== Checkpoint =====

    public async Task SaveCheckpointAsync(DownloadCheckpoint checkpoint, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        try
        {
            var taskIdStr = checkpoint.TaskId.Value.ToString();

            // Upsert checkpoint header
            await conn.ExecuteAsync("""
                INSERT OR REPLACE INTO download_checkpoints (task_id, manifest_json, saved_at)
                VALUES (@TaskId, @ManifestJson, @SavedAt)
                """,
                new { TaskId = taskIdStr, checkpoint.ManifestJson, SavedAt = checkpoint.SavedAt.ToString("O") },
                tx);

            // Delete old chunks then insert new
            await conn.ExecuteAsync(
                "DELETE FROM chunk_checkpoints WHERE task_id = @TaskId",
                new { TaskId = taskIdStr }, tx);

            foreach (var chunk in checkpoint.Chunks)
            {
                await conn.ExecuteAsync("""
                    INSERT INTO chunk_checkpoints (task_id, chunk_index, range_start, range_end, downloaded_bytes, is_completed, partial_file, hash)
                    VALUES (@TaskId, @ChunkIndex, @RangeStart, @RangeEnd, @DownloadedBytes, @IsCompleted, @PartialFile, @Hash)
                    """,
                    new
                    {
                        TaskId = taskIdStr,
                        chunk.ChunkIndex,
                        chunk.RangeStart,
                        chunk.RangeEnd,
                        chunk.DownloadedBytes,
                        IsCompleted = chunk.IsCompleted ? 1 : 0,
                        PartialFile = chunk.PartialFilePath,
                        chunk.Hash,
                    },
                    tx);
            }

            tx.Commit();
            _logger.Debug("保存 Checkpoint {TaskId}, {ChunkCount} 个分块", checkpoint.TaskId, checkpoint.Chunks.Count);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<DownloadCheckpoint?> GetCheckpointAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        var taskIdStr = taskId.Value.ToString();

        var header = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM download_checkpoints WHERE task_id = @TaskId",
            new { TaskId = taskIdStr });

        if (header is null) return null;

        var chunks = (await conn.QueryAsync<dynamic>(
            "SELECT * FROM chunk_checkpoints WHERE task_id = @TaskId ORDER BY chunk_index",
            new { TaskId = taskIdStr }))
            .Select(r => new ChunkCheckpoint
            {
                ChunkIndex = (int)(long)r.chunk_index,
                RangeStart = (long)r.range_start,
                RangeEnd = (long)r.range_end,
                DownloadedBytes = (long)r.downloaded_bytes,
                IsCompleted = (long)r.is_completed == 1,
                PartialFilePath = (string?)r.partial_file,
                Hash = (string?)r.hash,
            })
            .ToList();

        return new DownloadCheckpoint
        {
            TaskId = taskId,
            ManifestJson = (string)header.manifest_json,
            Chunks = chunks,
            SavedAt = DateTime.Parse((string)header.saved_at, System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    public async Task DeleteCheckpointAsync(DownloadTaskId taskId, CancellationToken ct)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(ct);
        using var transaction = conn.BeginTransaction();
        var taskIdStr = taskId.Value.ToString();
        await conn.ExecuteAsync("DELETE FROM chunk_checkpoints WHERE task_id = @TaskId", new { TaskId = taskIdStr }, transaction);
        await conn.ExecuteAsync("DELETE FROM download_checkpoints WHERE task_id = @TaskId", new { TaskId = taskIdStr }, transaction);
        transaction.Commit();
        _logger.Debug("删除 Checkpoint {TaskId}", taskId);
    }

    // ===== Mapping =====

    private static DownloadTask MapToDomain(DownloadRow row)
    {
        var state = Enum.Parse<DownloadState>(row.status, ignoreCase: true);
        // Map legacy status values
        if (row.status == "Pending") state = DownloadState.Queued;

        return new DownloadTask(
            new DownloadTaskId(Guid.Parse(row.id)),
            row.asset_id,
            row.asset_name,
            row.download_url,
            row.save_path,
            row.total_bytes,
            row.downloaded_bytes,
            state,
            priority: 0,
            retryCount: (int)row.retry_count,
            lastError: row.error_message,
            createdAt: DateTimeOffset.Parse(row.created_at, System.Globalization.CultureInfo.InvariantCulture),
            updatedAt: DateTimeOffset.Parse(row.updated_at, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static object MapToRow(DownloadTask task) => new
    {
        Id = task.Id.Value.ToString(),
        AssetId = task.AssetId,
        AssetName = task.DisplayName,
        DownloadUrl = task.DownloadUrl,
        SavePath = task.InstallPath,
        task.TotalBytes,
        task.DownloadedBytes,
        Status = task.State.ToString(),
        task.RetryCount,
        ErrorMessage = task.LastError,
        CreatedAt = task.CreatedAt.ToString("O"),
        UpdatedAt = task.UpdatedAt.ToString("O"),
    };

    // ===== Internal Row Type =====

    private sealed class DownloadRow
    {
        public string id { get; set; } = default!;
        public string asset_id { get; set; } = default!;
        public string asset_name { get; set; } = default!;
        public string download_url { get; set; } = default!;
        public string save_path { get; set; } = default!;
        public long total_bytes { get; set; }
        public long downloaded_bytes { get; set; }
        public string status { get; set; } = default!;
        public long retry_count { get; set; }
        public string? error_message { get; set; }
        public string created_at { get; set; } = default!;
        public string updated_at { get; set; } = default!;
    }
}
