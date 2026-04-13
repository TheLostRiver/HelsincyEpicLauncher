// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Common;

namespace Launcher.Domain.Installations;

/// <summary>
/// 安装状态机。定义 8 个状态及合法转换规则。
/// </summary>
public sealed class InstallStateMachine : StateMachine<InstallState>
{
    public InstallStateMachine() : this(InstallState.NotInstalled) { }

    public InstallStateMachine(InstallState initialState) : base(initialState)
    {
        // NotInstalled → Installing
        DefineTransition(InstallState.NotInstalled, InstallState.Installing);

        // Installing → Installed | Failed
        DefineTransition(InstallState.Installing, InstallState.Installed);
        DefineTransition(InstallState.Installing, InstallState.Failed);

        // Installed → Verifying | Uninstalling
        DefineTransition(InstallState.Installed, InstallState.Verifying);
        DefineTransition(InstallState.Installed, InstallState.Uninstalling);

        // Verifying → Installed | NeedsRepair | Failed
        DefineTransition(InstallState.Verifying, InstallState.Installed);
        DefineTransition(InstallState.Verifying, InstallState.NeedsRepair);
        DefineTransition(InstallState.Verifying, InstallState.Failed);

        // NeedsRepair → Repairing | Uninstalling
        DefineTransition(InstallState.NeedsRepair, InstallState.Repairing);
        DefineTransition(InstallState.NeedsRepair, InstallState.Uninstalling);

        // Repairing → Installed | Failed
        DefineTransition(InstallState.Repairing, InstallState.Installed);
        DefineTransition(InstallState.Repairing, InstallState.Failed);

        // Uninstalling → NotInstalled | Failed
        DefineTransition(InstallState.Uninstalling, InstallState.NotInstalled);
        DefineTransition(InstallState.Uninstalling, InstallState.Failed);

        // Failed → Installing | Uninstalling (可重试安装或选择卸载)
        DefineTransition(InstallState.Failed, InstallState.Installing);
        DefineTransition(InstallState.Failed, InstallState.Uninstalling);
    }
}
