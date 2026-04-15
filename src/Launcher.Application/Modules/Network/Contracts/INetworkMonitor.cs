// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Network.Contracts;

/// <summary>
/// 网络状态监视器。提供实时网络可用性检测。
/// </summary>
public interface INetworkMonitor
{
    /// <summary>当前网络是否可用</summary>
    bool IsNetworkAvailable { get; }

    /// <summary>
    /// 网络状态变化事件。true = 上线，false = 下线。
    /// </summary>
    event Action<bool>? NetworkStatusChanged;
}
