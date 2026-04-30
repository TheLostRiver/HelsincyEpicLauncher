// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Background.Hosting;

/// <summary>
/// 后台 Worker 的统一宿主。
/// </summary>
public interface IBackgroundTaskHost
{
    IReadOnlyList<IBackgroundWorker> Workers { get; }

    IReadOnlyDictionary<string, Exception> Faults { get; }

    Task StartAllAsync(CancellationToken ct = default);

    Task StopAllAsync(CancellationToken ct = default);
}
