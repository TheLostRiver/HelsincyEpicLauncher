// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Serilog;

namespace Launcher.Background.Network;

/// <summary>
/// 网络监视后台服务。订阅 INetworkMonitor 事件，网络断联时暂停所有下载，网络恢复后自动续传。
/// 不持有 Infrastructure 引用，仅依赖 Application 层契约。
/// </summary>
public sealed class NetworkMonitorWorker : IDisposable
{
    private readonly INetworkMonitor _networkMonitor;
    private readonly IDownloadCommandService _downloadCommandService;
    private readonly ILogger _logger = Log.ForContext<NetworkMonitorWorker>();
    private bool _disposed;

    public NetworkMonitorWorker(
        INetworkMonitor networkMonitor,
        IDownloadCommandService downloadCommandService)
    {
        _networkMonitor = networkMonitor;
        _downloadCommandService = downloadCommandService;
    }

    /// <summary>启动监听</summary>
    public void Start()
    {
        _networkMonitor.NetworkStatusChanged += OnNetworkStatusChanged;
        _logger.Information("网络监视服务已启动 | 当前网络状态={IsAvailable}", _networkMonitor.IsNetworkAvailable);
    }

    /// <summary>停止监听</summary>
    public void Stop()
    {
        _networkMonitor.NetworkStatusChanged -= OnNetworkStatusChanged;
        _logger.Information("网络监视服务已停止");
    }

    private async void OnNetworkStatusChanged(bool isAvailable)
    {
        // async void 仅限于事件回调，内部 try/catch 防止异常逃逸
        try
        {
            if (isAvailable)
            {
                _logger.Information("网络恢复，自动续传所有已暂停下载");
                var result = await _downloadCommandService.ResumeAllAsync(CancellationToken.None);
                if (!result.IsSuccess)
                    _logger.Warning("续传下载失败 | Error={Error}", result.Error?.TechnicalMessage);
            }
            else
            {
                _logger.Warning("网络断联，暂停所有活跃下载");
                var result = await _downloadCommandService.PauseAllAsync(CancellationToken.None);
                if (!result.IsSuccess)
                    _logger.Warning("暂停下载失败 | Error={Error}", result.Error?.TechnicalMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "处理网络状态变化时发生异常 | IsAvailable={IsAvailable}", isAvailable);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
