// Copyright (c) Helsincy. All rights reserved.

using System.Security.Cryptography;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.Installations;

/// <summary>
/// SHA-256 哈希计算服务。支持并行多文件计算。
/// </summary>
public sealed class HashingService : IHashingService
{
    private readonly ILogger _logger = Log.ForContext<HashingService>();

    public async Task<Result<string>> ComputeHashAsync(string filePath, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Result.Fail<string>(new Error
                {
                    Code = "HASH_FILE_NOT_FOUND",
                    UserMessage = "文件不存在",
                    TechnicalMessage = $"文件不存在: {filePath}",
                    CanRetry = false,
                    Severity = ErrorSeverity.Error,
                });
            }

            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var hashBytes = await sha256.ComputeHashAsync(stream, ct);
            return Result.Ok(Convert.ToHexStringLower(hashBytes));
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<string>(new Error
            {
                Code = "HASH_CANCELLED",
                UserMessage = "哈希计算被取消",
                TechnicalMessage = "OperationCanceledException",
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            });
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "哈希计算 I/O 错误 {Path}", filePath);
            return Result.Fail<string>(new Error
            {
                Code = "HASH_IO_ERROR",
                UserMessage = "文件读取失败",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> ComputeHashesAsync(
        IReadOnlyList<string> filePaths,
        int maxParallelism,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        var results = new Dictionary<string, string>();
        var completed = 0;
        using var semaphore = new SemaphoreSlim(maxParallelism);
        var lockObj = new object();
        var tasks = new List<Task>();

        foreach (var filePath in filePaths)
        {
            ct.ThrowIfCancellationRequested();

            await semaphore.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var result = await ComputeHashAsync(filePath, ct);
                    if (result.IsSuccess)
                    {
                        lock (lockObj)
                        {
                            results[filePath] = result.Value!;
                        }
                    }

                    var count = Interlocked.Increment(ref completed);
                    progress?.Report(count);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<IReadOnlyDictionary<string, string>>(new Error
            {
                Code = "HASH_BATCH_CANCELLED",
                UserMessage = "批量哈希计算被取消",
                TechnicalMessage = "OperationCanceledException in batch",
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            });
        }

        _logger.Debug("批量哈希完成: {Count}/{Total}", results.Count, filePaths.Count);
        return Result.Ok<IReadOnlyDictionary<string, string>>(results);
    }
}
