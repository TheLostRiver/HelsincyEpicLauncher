// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载子系统的运行时配置。
/// </summary>
public sealed class DownloadOptions
{
    public const int DefaultMaxConcurrentTasks = 3;
    public const int DefaultMaxConcurrentChunksPerTask = 4;
    public const long DefaultChunkSizeBytes = 10L * 1024 * 1024;
    public const int DefaultMaxRetryAttempts = 5;
    public static readonly TimeSpan DefaultCheckpointInterval = TimeSpan.FromSeconds(30);

    public int MaxConcurrentTasks { get; init; } = DefaultMaxConcurrentTasks;
    public int MaxConcurrentChunksPerTask { get; init; } = DefaultMaxConcurrentChunksPerTask;
    public long ChunkSizeBytes { get; init; } = DefaultChunkSizeBytes;
    public int MaxRetryAttempts { get; init; } = DefaultMaxRetryAttempts;
    public TimeSpan CheckpointInterval { get; init; } = DefaultCheckpointInterval;
}

/// <summary>
/// 下载配置读取端口。由 Application 定义，Infrastructure 从具体配置源实现。
/// </summary>
public interface IDownloadOptionsProvider
{
    DownloadOptions DownloadOptions { get; }
}
