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

/// <summary>
/// 日志级别
/// </summary>
public enum LogEntryLevel
{
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}

/// <summary>
/// 日志条目
/// </summary>
public sealed class LogEntry
{
    /// <summary>时间戳</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>日志级别</summary>
    public LogEntryLevel Level { get; init; }

    /// <summary>来源模块（SourceContext）</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>日志消息</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>异常信息（如有）</summary>
    public string? Exception { get; init; }

    /// <summary>关联 ID</summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// 缓存使用统计
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>缩略图缓存大小（字节）</summary>
    public long ThumbnailCacheBytes { get; init; }

    /// <summary>缩略图缓存文件数</summary>
    public int ThumbnailFileCount { get; init; }

    /// <summary>搜索/Manifest 缓存大小（字节）</summary>
    public long ManifestCacheBytes { get; init; }

    /// <summary>Manifest 缓存文件数</summary>
    public int ManifestFileCount { get; init; }

    /// <summary>日志文件总大小（字节）</summary>
    public long LogFileBytes { get; init; }

    /// <summary>日志文件数</summary>
    public int LogFileCount { get; init; }

    /// <summary>总缓存大小（字节）</summary>
    public long TotalBytes => ThumbnailCacheBytes + ManifestCacheBytes + LogFileBytes;
}
