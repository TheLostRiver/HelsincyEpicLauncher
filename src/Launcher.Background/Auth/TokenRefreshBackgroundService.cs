// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Serilog;

namespace Launcher.Background.Auth;

/// <summary>
/// Token 自动刷新后台服务。定期检查 Token 有效性，在过期前 5 分钟主动刷新。
/// </summary>
public sealed class TokenRefreshBackgroundService : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<TokenRefreshBackgroundService>();
    private readonly IAuthService _authService;
    private Timer? _timer;
    private bool _disposed;

    /// <summary>
    /// 检查间隔（默认 2 分钟检查一次）
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);

    public TokenRefreshBackgroundService(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// 启动定时刷新
    /// </summary>
    public void Start()
    {
        if (_timer is not null)
            return;

        _timer = new Timer(OnTimerTick, null, CheckInterval, CheckInterval);
        _logger.Information("Token 自动刷新服务已启动 | 间隔={Interval}秒", CheckInterval.TotalSeconds);
    }

    /// <summary>
    /// 停止定时刷新
    /// </summary>
    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.Information("Token 自动刷新服务已停止");
    }

    private async void OnTimerTick(object? state)
    {
        // 防止并发重叠：回调期间停止定时器
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);

        try
        {
            if (!_authService.IsAuthenticated)
                return;

            // GetAccessTokenAsync 内部已有过期前 5 分钟主动刷新的逻辑
            var result = await _authService.GetAccessTokenAsync();
            if (!result.IsSuccess)
            {
                _logger.Warning("Token 自动刷新失败 | Error={Error}", result.Error?.TechnicalMessage);
            }
            else
            {
                _logger.Debug("Token 自动刷新检查完成（有效）");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Token 自动刷新定时器异常");
        }
        finally
        {
            // 回调完成后重启定时器
            if (!_disposed)
                _timer?.Change(CheckInterval, CheckInterval);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
