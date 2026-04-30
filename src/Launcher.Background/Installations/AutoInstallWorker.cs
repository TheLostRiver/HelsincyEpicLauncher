// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Background.Hosting;
using Launcher.Shared.Configuration;
using Serilog;

namespace Launcher.Background.Installations;

/// <summary>
/// 下载完成后自动安装 Worker。监听 DownloadCompleted 事件，
/// 根据用户设置决定是否自动触发安装。
/// 通信路径：DownloadRuntimeStore.DownloadCompleted → AutoInstallWorker → IInstallCommandService.InstallAsync
/// 所有依赖均为 Contracts 接口，零模块内部耦合。
/// </summary>
public sealed class AutoInstallWorker : IBackgroundWorker, IDisposable
{
    private readonly IDownloadRuntimeStore _runtimeStore;
    private readonly IDownloadReadService _downloadReadService;
    private readonly ISettingsReadService _settingsReadService;
    private readonly IInstallCommandService _installCommandService;
    private readonly IAppConfigProvider _configProvider;
    private readonly ILogger _logger = Log.ForContext<AutoInstallWorker>();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public AutoInstallWorker(
        IDownloadRuntimeStore runtimeStore,
        IDownloadReadService downloadReadService,
        ISettingsReadService settingsReadService,
        IInstallCommandService installCommandService,
        IAppConfigProvider configProvider)
    {
        _runtimeStore = runtimeStore;
        _downloadReadService = downloadReadService;
        _settingsReadService = settingsReadService;
        _installCommandService = installCommandService;
        _configProvider = configProvider;
    }

    public string Name => nameof(AutoInstallWorker);

    public WorkerStatus State { get; private set; } = WorkerStatus.Idle;

    /// <summary>启动监听</summary>
    public void Start()
        => StartAsync(CancellationToken.None).GetAwaiter().GetResult();

    public Task StartAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_cts is { IsCancellationRequested: false })
        {
            State = WorkerStatus.Running;
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _runtimeStore.DownloadCompleted -= OnDownloadCompleted;
        _runtimeStore.DownloadCompleted += OnDownloadCompleted;
        State = WorkerStatus.Running;
        _logger.Information("AutoInstallWorker 已启动");
        return Task.CompletedTask;
    }

    /// <summary>停止监听</summary>
    public void Stop()
        => StopAsync(CancellationToken.None).GetAwaiter().GetResult();

    private async void OnDownloadCompleted(DownloadCompletedEvent evt)
    {
        if (_cts is null || _cts.IsCancellationRequested) return;
        var ct = _cts.Token;

        try
        {
            var config = _settingsReadService.GetDownloadConfig();
            if (!config.AutoInstall)
            {
                _logger.Debug("自动安装已关闭，跳过 {AssetId}", evt.AssetId);
                return;
            }

            _logger.Information("自动安装触发 {AssetId}, 下载文件: {Path}", evt.AssetId, evt.DownloadedFilePath);

            // 获取资产名称
            var status = await _downloadReadService.GetStatusAsync(evt.AssetId, ct);
            var assetName = status?.AssetName ?? evt.AssetId;

            var request = new InstallRequest
            {
                AssetId = evt.AssetId,
                AssetName = assetName,
                SourcePath = evt.DownloadedFilePath,
                InstallPath = Path.Combine(_configProvider.InstallPath, evt.AssetId),
            };

            var result = await _installCommandService.InstallAsync(request, ct);

            if (result.IsSuccess)
                _logger.Information("自动安装成功 {AssetId}", evt.AssetId);
            else
                _logger.Warning("自动安装失败 {AssetId}: {Error}", evt.AssetId, result.Error?.TechnicalMessage);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("自动安装已取消 {AssetId}", evt.AssetId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "自动安装异常 {AssetId}", evt.AssetId);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopCore();
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        State = WorkerStatus.Stopping;
        StopCore();
        State = WorkerStatus.Stopped;
        _logger.Debug("AutoInstallWorker 已停止");
        return Task.CompletedTask;
    }

    private void StopCore()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _runtimeStore.DownloadCompleted -= OnDownloadCompleted;
        State = WorkerStatus.Stopped;
    }
}
