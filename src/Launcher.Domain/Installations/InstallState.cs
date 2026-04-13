// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Domain.Installations;

/// <summary>
/// 安装状态枚举（内部细粒度状态）
/// </summary>
public enum InstallState
{
    NotInstalled,
    Installing,
    Installed,
    Verifying,
    NeedsRepair,
    Repairing,
    Uninstalling,
    Failed,
}
