// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Updates.Contracts;
using Launcher.Background.Hosting;
using Serilog;

namespace Launcher.Background.Updates;

/// <summary>
/// 自动更新检查后台服务。每隔固定时间检查一次是否有新版本。
/// 发现新版本时通过 IInternalUpdateNotifier 触发 IAppUpdateService.UpdateAvailable 事件，
/// 订阅方（如 ShellViewModel）无需了解 Worker 实现。
/// 该 Worker 仅负责定时触发检查——不持有 UI 引用，不跨模块耦合。
/// </summary>
public sealed class AppUpdateWorker : IBackgroundWorker, IDisposable
{
    // 文档规格：默认 24 小时检查一次
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    // 启动后延迟首次检查，避免与启动阶段竞争资源
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    private readonly IAppUpdateService _updateService;
    private readonly ILogger _logger = Log.ForContext<AppUpdateWorker>();
    private Timer? _timer;
    private bool _disposed;

    public AppUpdateWorker(IAppUpdateService updateService)
    {
        _updateService = updateService;
    }

    public string Name => nameof(AppUpdateWorker);

    public WorkerStatus State { get; private set; } = WorkerStatus.Idle;

    /// <summary>启动定时检查</summary>
    public void Start()
        => StartAsync(CancellationToken.None).GetAwaiter().GetResult();

    public Task StartAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_timer is not null)
        {
            State = WorkerStatus.Running;
            return Task.CompletedTask;
        }

        _timer = new Timer(OnTimerTickAsync, null, InitialDelay, CheckInterval);
        State = WorkerStatus.Running;
        _logger.Information("自动更新检查服务已启动 | 首次检查延迟={Delay}分钟 | 间隔={Interval}小时",
            InitialDelay.TotalMinutes, CheckInterval.TotalHours);
        return Task.CompletedTask;
    }

    /// <summary>停止定时检查</summary>
    public void Stop()
        => StopAsync(CancellationToken.None).GetAwaiter().GetResult();

    public Task StopAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        State = WorkerStatus.Stopping;
        StopCore();
        State = WorkerStatus.Stopped;
        _logger.Information("自动更新检查服务已停止");
        return Task.CompletedTask;
    }

    /// <summary>立即触发一次检查（供外部主动调用）</summary>
    public Task CheckNowAsync(CancellationToken ct = default)
        => PerformCheckAsync(ct);

    private async void OnTimerTickAsync(object? state)
    {
        // async void 仅限于 Timer 回调，内部 try/catch 防止异常逃逸
        await PerformCheckAsync(CancellationToken.None);
    }

    private async Task PerformCheckAsync(CancellationToken ct)
    {
        try
        {
            _logger.Debug("开始执行更新检查");
            var result = await _updateService.CheckForUpdateAsync(ct);

            if (!result.IsSuccess)
            {
                _logger.Warning("更新检查返回失败 | Error={Error}", result.Error?.TechnicalMessage);
                return;
            }

            if (result.Value is null)
            {
                _logger.Debug("已是最新版本，无需更新");
                return;
            }

            // 通过 IInternalUpdateNotifier 接口触发事件，避免 Background → Infrastructure 跨层耦合
            var info = result.Value;
            _logger.Information("发现新版本，触发更新事件 | 版本={Version} | 强制={IsMandatory}",
                info.Version, info.IsMandatory);

            if (_updateService is IInternalUpdateNotifier notifier)
            {
                notifier.NotifyUpdateAvailable(new UpdateAvailableEvent(info.Version, info.IsMandatory));
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "更新检查时发生异常");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopCore();
        State = WorkerStatus.Stopped;
    }

    private void StopCore()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _timer?.Dispose();
        _timer = null;
    }
}
