// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Background.Hosting;

/// <summary>
/// 后台 Worker 的统一生命周期状态。
/// </summary>
public enum WorkerStatus
{
    Idle,
    Running,
    Stopping,
    Stopped,
    Faulted,
}
