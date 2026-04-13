// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Diagnostics.Contracts;

/// <summary>
/// 系统诊断摘要信息
/// </summary>
public sealed class SystemDiagnosticsSummary
{
    /// <summary>可用磁盘空间（MB）</summary>
    public long AvailableDiskSpaceMb { get; init; }

    /// <summary>总磁盘空间（MB）</summary>
    public long TotalDiskSpaceMb { get; init; }

    /// <summary>已用内存（MB）</summary>
    public long UsedMemoryMb { get; init; }

    /// <summary>总物理内存（MB）</summary>
    public long TotalMemoryMb { get; init; }

    /// <summary>操作系统版本</summary>
    public string OsVersion { get; init; } = string.Empty;

    /// <summary>.NET 运行时版本</summary>
    public string DotNetVersion { get; init; } = string.Empty;

    /// <summary>应用版本</summary>
    public string AppVersion { get; init; } = string.Empty;

    /// <summary>应用启动时间</summary>
    public DateTime AppStartedAt { get; init; }

    /// <summary>进程占用内存（MB）</summary>
    public long ProcessMemoryMb { get; init; }

    /// <summary>数据库文件大小（MB）</summary>
    public long DatabaseSizeMb { get; init; }
}
