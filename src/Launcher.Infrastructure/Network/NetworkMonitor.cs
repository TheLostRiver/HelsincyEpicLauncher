// Copyright (c) Helsincy. All rights reserved.

using System.Net.NetworkInformation;
using Launcher.Application.Modules.Network.Contracts;
using Serilog;

namespace Launcher.Infrastructure.Network;

/// <summary>
/// 网络状态监视器实现。通过系统 NetworkChange 事件监听网络可用性变化。
/// </summary>
public sealed class NetworkMonitor : INetworkMonitor, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<NetworkMonitor>();
    private bool _isNetworkAvailable;

    public NetworkMonitor()
    {
        _isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _logger.Information("网络监视器初始化 | IsAvailable={IsAvailable}", _isNetworkAvailable);
    }

    /// <inheritdoc/>
    public bool IsNetworkAvailable => _isNetworkAvailable;

    /// <inheritdoc/>
    public event Action<bool>? NetworkStatusChanged;

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        var isAvailable = e.IsAvailable;
        if (isAvailable == _isNetworkAvailable)
            return;

        _isNetworkAvailable = isAvailable;
        _logger.Information("网络状态变化 | IsAvailable={IsAvailable}", isAvailable);
        NetworkStatusChanged?.Invoke(isAvailable);
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }
}
