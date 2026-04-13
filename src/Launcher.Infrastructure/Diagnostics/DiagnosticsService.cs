// Copyright (c) Helsincy. All rights reserved.

using System.Diagnostics;
using Launcher.Application.Modules.Diagnostics.Contracts;
using Launcher.Shared.Configuration;
using Serilog;

namespace Launcher.Infrastructure.Diagnostics;

/// <summary>
/// 诊断服务实现。收集系统信息、磁盘空间、内存使用等诊断数据。
/// </summary>
internal sealed class DiagnosticsService : IDiagnosticsReadService
{
    private static readonly ILogger Logger = Log.ForContext<DiagnosticsService>();
    private static readonly DateTime AppStartTime = DateTime.UtcNow;

    private readonly IAppConfigProvider _configProvider;

    public DiagnosticsService(IAppConfigProvider configProvider)
    {
        _configProvider = configProvider;
        Logger.Debug("诊断服务已初始化");
    }

    public Task<SystemDiagnosticsSummary> GetSystemSummaryAsync(CancellationToken ct = default)
    {
        var process = Process.GetCurrentProcess();

        // 磁盘空间（数据目录所在磁盘）
        long availableDiskMb = 0;
        long totalDiskMb = 0;
        try
        {
            var dataPath = _configProvider.DataPath;
            var driveRoot = Path.GetPathRoot(dataPath);
            if (!string.IsNullOrEmpty(driveRoot))
            {
                var driveInfo = new DriveInfo(driveRoot);
                availableDiskMb = driveInfo.AvailableFreeSpace / (1024 * 1024);
                totalDiskMb = driveInfo.TotalSize / (1024 * 1024);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "获取磁盘空间信息失败");
        }

        // 内存信息
        long totalMemoryMb = 0;
        long usedMemoryMb = 0;
        try
        {
            totalMemoryMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            usedMemoryMb = totalMemoryMb - (long)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes - process.WorkingSet64) / (1024 * 1024);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "获取内存信息失败");
        }

        // 数据库文件大小
        long dbSizeMb = 0;
        try
        {
            var dbPath = Path.Combine(_configProvider.DataPath, "launcher.db");
            if (File.Exists(dbPath))
            {
                dbSizeMb = new FileInfo(dbPath).Length / (1024 * 1024);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "获取数据库文件大小失败");
        }

        var summary = new SystemDiagnosticsSummary
        {
            OsVersion = $"{Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})",
            DotNetVersion = Environment.Version.ToString(),
            AppVersion = _configProvider.AppVersion,
            AppStartedAt = AppStartTime,
            AvailableDiskSpaceMb = availableDiskMb,
            TotalDiskSpaceMb = totalDiskMb,
            TotalMemoryMb = totalMemoryMb,
            UsedMemoryMb = usedMemoryMb,
            ProcessMemoryMb = process.WorkingSet64 / (1024 * 1024),
            DatabaseSizeMb = dbSizeMb,
        };

        Logger.Debug("系统诊断摘要已生成 | OS={Os} | 可用磁盘={DiskMb}MB | 进程内存={MemMb}MB",
            summary.OsVersion, summary.AvailableDiskSpaceMb, summary.ProcessMemoryMb);

        return Task.FromResult(summary);
    }
}
