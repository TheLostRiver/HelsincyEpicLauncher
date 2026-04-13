// Copyright (c) Helsincy. All rights reserved.

using System.IO.Compression;
using System.Security.Cryptography;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Domain.Installations;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.Installations;

/// <summary>
/// 安装 Worker。负责将下载的文件解压/复制到安装目录，并生成 Manifest。
/// </summary>
public sealed class InstallWorker
{
    private readonly IInstallationRepository _repository;
    private readonly ILogger _logger = Log.ForContext<InstallWorker>();

    public InstallWorker(IInstallationRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 执行安装。将源文件解压到目标路径，生成 Manifest，更新数据库记录。
    /// </summary>
    public async Task<Result> ExecuteAsync(
        Installation installation,
        string sourcePath,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        _logger.Information("开始安装 {AssetId} → {InstallPath}", installation.AssetId, installation.InstallPath);

        try
        {
            // 验证源文件存在
            if (!File.Exists(sourcePath))
            {
                return Result.Fail(new Error
                {
                    Code = "INSTALL_SOURCE_NOT_FOUND",
                    UserMessage = "下载文件不存在",
                    TechnicalMessage = $"源文件不存在: {sourcePath}",
                    CanRetry = false,
                    Severity = ErrorSeverity.Error,
                });
            }

            // 验证目标路径（磁盘空间）
            var spaceCheck = CheckDiskSpace(sourcePath, installation.InstallPath);
            if (!spaceCheck.IsSuccess)
                return spaceCheck;

            // 验证/创建目标目录
            var installDir = installation.InstallPath;
            if (!Directory.Exists(installDir))
                Directory.CreateDirectory(installDir);

            // 解压或复制
            var files = new List<ManifestFileEntry>();
            if (IsZipFile(sourcePath))
            {
                files = await ExtractZipAsync(sourcePath, installDir, progress, ct);
            }
            else
            {
                files = await CopySingleFileAsync(sourcePath, installDir, progress, ct);
            }

            ct.ThrowIfCancellationRequested();

            // 计算安装总大小
            long totalSize = files.Sum(f => f.Size);
            installation.SetSize(totalSize);

            // 生成并保存 Manifest
            var manifest = new InstallManifest
            {
                AssetId = installation.AssetId,
                Version = installation.Version,
                Files = files,
                TotalSize = totalSize,
            };
            await _repository.SaveManifestAsync(installation.AssetId, manifest, ct);

            // 更新数据库
            await _repository.UpdateAsync(installation, ct);

            _logger.Information("安装完成 {AssetId}, 共 {FileCount} 个文件, {Size} 字节",
                installation.AssetId, files.Count, totalSize);

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("安装被取消 {AssetId}", installation.AssetId);
            return Result.Fail(new Error
            {
                Code = "INSTALL_CANCELLED",
                UserMessage = "安装已取消",
                TechnicalMessage = "OperationCanceledException",
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "安装权限不足 {AssetId} → {Path}", installation.AssetId, installation.InstallPath);
            return Result.Fail(new Error
            {
                Code = "INSTALL_ACCESS_DENIED",
                UserMessage = "安装路径权限不足，请检查文件夹权限",
                TechnicalMessage = ex.Message,
                CanRetry = false,
                Severity = ErrorSeverity.Error,
            });
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "安装 I/O 错误 {AssetId}", installation.AssetId);
            return Result.Fail(new Error
            {
                Code = "INSTALL_IO_ERROR",
                UserMessage = "安装时发生文件读写错误",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }
    }

    private static Result CheckDiskSpace(string sourcePath, string installPath)
    {
        try
        {
            var sourceSize = new FileInfo(sourcePath).Length;
            var requiredSpace = (long)(sourceSize * 2.5); // 解压需要额外空间

            var root = Path.GetPathRoot(installPath);
            if (string.IsNullOrEmpty(root))
                return Result.Ok();

            var driveInfo = new DriveInfo(root);
            if (driveInfo.AvailableFreeSpace < requiredSpace)
            {
                return Result.Fail(new Error
                {
                    Code = "INSTALL_DISK_FULL",
                    UserMessage = $"磁盘空间不足。需要 {requiredSpace / 1048576} MB，可用 {driveInfo.AvailableFreeSpace / 1048576} MB",
                    TechnicalMessage = $"Required: {requiredSpace}, Available: {driveInfo.AvailableFreeSpace}",
                    CanRetry = false,
                    Severity = ErrorSeverity.Error,
                });
            }
            return Result.Ok();
        }
        catch
        {
            // 无法检查磁盘空间时不阻断安装
            return Result.Ok();
        }
    }

    private static bool IsZipFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var header = new byte[4];
            if (stream.Read(header, 0, 4) < 4) return false;
            // ZIP magic number: PK\x03\x04
            return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<List<ManifestFileEntry>> ExtractZipAsync(
        string zipPath,
        string targetDir,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var files = new List<ManifestFileEntry>();

        using var archive = ZipFile.OpenRead(zipPath);
        var totalEntries = archive.Entries.Count(e => !string.IsNullOrEmpty(e.Name));
        var processed = 0;

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.Name))
                continue; // 目录条目

            var destPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

            // 防止 Zip Slip 攻击
            if (!destPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                continue;

            var destDir = Path.GetDirectoryName(destPath)!;
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            entry.ExtractToFile(destPath, overwrite: true);

            // 计算哈希
            var hash = await ComputeFileHashAsync(destPath, ct);
            files.Add(new ManifestFileEntry
            {
                RelativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar),
                Size = entry.Length,
                Hash = hash,
            });

            processed++;
            progress?.Report(new InstallProgress
            {
                ProcessedFiles = processed,
                TotalFiles = totalEntries,
                CurrentFile = entry.FullName,
            });
        }

        return files;
    }

    private static async Task<List<ManifestFileEntry>> CopySingleFileAsync(
        string sourcePath,
        string targetDir,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(targetDir, fileName);
        File.Copy(sourcePath, destPath, overwrite: true);

        var hash = await ComputeFileHashAsync(destPath, ct);
        var info = new FileInfo(destPath);

        progress?.Report(new InstallProgress
        {
            ProcessedFiles = 1,
            TotalFiles = 1,
            CurrentFile = fileName,
        });

        return
        [
            new ManifestFileEntry
            {
                RelativePath = fileName,
                Size = info.Length,
                Hash = hash,
            }
        ];
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexStringLower(hashBytes);
    }
}

/// <summary>
/// 安装进度报告
/// </summary>
public sealed class InstallProgress
{
    public int ProcessedFiles { get; init; }
    public int TotalFiles { get; init; }
    public required string CurrentFile { get; init; }
}
