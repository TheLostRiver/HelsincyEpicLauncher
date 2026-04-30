// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Background.Hosting;

/// <summary>
/// 后台任务的统一生命周期契约。
/// </summary>
public interface IBackgroundWorker
{
    string Name { get; }

    WorkerStatus State { get; }

    Task StartAsync(CancellationToken ct = default);

    Task StopAsync(CancellationToken ct = default);
}
