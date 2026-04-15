// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Domain.Installations;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.Installations;

/// <summary>
/// 文件完整性校验器。逐文件哈希比对，支持并行计算。
/// </summary>
public sealed class IntegrityVerifier : IIntegrityVerifier
{
    private readonly IHashingService _hashingService;
    private readonly ILogger _logger = Log.ForContext<IntegrityVerifier>();
    private const int MaxParallelism = 4;

    public IntegrityVerifier(IHashingService hashingService)
    {
        _hashingService = hashingService;
    }

    public async Task<Result<bool>> VerifyFileAsync(string filePath, string expectedHash, CancellationToken ct)
    {
        var result = await _hashingService.ComputeHashAsync(filePath, ct);
        if (!result.IsSuccess)
            return Result.Fail<bool>(result.Error!);

        var match = string.Equals(result.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        return Result.Ok(match);
    }

    public async Task<Result<VerificationReport>> VerifyInstallationAsync(
        string installPath,
        InstallManifest manifest,
        IProgress<VerificationProgress>? progress,
        CancellationToken ct)
    {
        _logger.Information("开始校验安装 {AssetId}, {FileCount} 个文件", manifest.AssetId, manifest.Files.Count);

        var missingFiles = new List<string>();
        var corruptedFiles = new List<string>();
        var totalFiles = manifest.Files.Count;
        var checkedFiles = 0L;

        // 第一遍：检查缺失文件
        foreach (var entry in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(installPath, entry.RelativePath);
            if (!File.Exists(fullPath))
            {
                missingFiles.Add(entry.RelativePath);
                checkedFiles++;
                progress?.Report(new VerificationProgress
                {
                    CheckedFiles = checkedFiles,
                    TotalFiles = totalFiles,
                    CurrentFile = entry.RelativePath,
                });
            }
        }

        // 第二遍：并行哈希校验存在的文件
        var filesToCheck = manifest.Files
            .Where(f => !missingFiles.Contains(f.RelativePath))
            .ToList();

        using var semaphore = new SemaphoreSlim(MaxParallelism);
        var tasks = new List<Task>();
        var lockObj = new object();

        foreach (var entry in filesToCheck)
        {
            ct.ThrowIfCancellationRequested();

            await semaphore.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var fullPath = Path.Combine(installPath, entry.RelativePath);
                    var hashResult = await _hashingService.ComputeHashAsync(fullPath, ct);

                    if (!hashResult.IsSuccess || !string.Equals(hashResult.Value, entry.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        lock (lockObj)
                        {
                            corruptedFiles.Add(entry.RelativePath);
                        }
                    }

                    var count = Interlocked.Increment(ref checkedFiles);
                    progress?.Report(new VerificationProgress
                    {
                        CheckedFiles = count,
                        TotalFiles = totalFiles,
                        CurrentFile = entry.RelativePath,
                    });
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
            return Result.Fail<VerificationReport>(new Error
            {
                Code = "VERIFY_CANCELLED",
                UserMessage = "校验被取消",
                TechnicalMessage = "OperationCanceledException during verification",
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            });
        }

        var isValid = missingFiles.Count == 0 && corruptedFiles.Count == 0;

        _logger.Information("校验完成 {AssetId}: Valid={IsValid}, Missing={Missing}, Corrupted={Corrupted}",
            manifest.AssetId, isValid, missingFiles.Count, corruptedFiles.Count);

        return Result.Ok(new VerificationReport
        {
            IsValid = isValid,
            MissingFiles = missingFiles,
            CorruptedFiles = corruptedFiles,
            TotalFilesChecked = checkedFiles,
        });
    }
}
