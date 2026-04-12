// Copyright (c) Helsincy. All rights reserved.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace Launcher.Presentation.Shell;

/// <summary>
/// Toast 通知服务实现。使用 WinUI 3 InfoBar 在窗口右上角显示通知。
/// 支持 Info / Warning / Error / Success 四种样式，自动消失 + 手动关闭。
/// </summary>
public sealed class NotificationService : INotificationService
{
    private static readonly ILogger Logger = Log.ForContext<NotificationService>();

    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan WarningDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(8);

    private Panel? _host;
    private DispatcherQueue? _dispatcherQueue;

    /// <summary>
    /// 设置 Toast 宿主面板。由 ShellPage 在加载时调用。
    /// </summary>
    public void SetHost(Panel host)
    {
        _host = host;
        _dispatcherQueue = host.DispatcherQueue;
        Logger.Debug("NotificationService 宿主面板已设置");
    }

    public void ShowSuccess(string message, TimeSpan? duration = null)
    {
        Show(message, InfoBarSeverity.Success, duration ?? DefaultDuration);
    }

    public void ShowWarning(string message, TimeSpan? duration = null)
    {
        Show(message, InfoBarSeverity.Warning, duration ?? WarningDuration);
    }

    public void ShowError(string message, TimeSpan? duration = null)
    {
        Show(message, InfoBarSeverity.Error, duration ?? ErrorDuration);
    }

    public void ShowInfo(string message, TimeSpan? duration = null)
    {
        Show(message, InfoBarSeverity.Informational, duration ?? DefaultDuration);
    }

    private void Show(string message, InfoBarSeverity severity, TimeSpan duration)
    {
        if (_host is null || _dispatcherQueue is null)
        {
            Logger.Warning("通知显示失败：宿主面板未设置 | {Severity} {Message}", severity, message);
            return;
        }

        Logger.Information("显示通知 | {Severity} {Message}", severity, message);

        _dispatcherQueue.TryEnqueue(() =>
        {
            var infoBar = new InfoBar
            {
                Message = message,
                Severity = severity,
                IsOpen = true,
                IsClosable = true,
            };

            // 手动关闭
            infoBar.CloseButtonClick += (_, _) =>
            {
                _host.Children.Remove(infoBar);
            };

            _host.Children.Add(infoBar);

            // 自动消失
            _ = DismissAfterDelayAsync(infoBar, duration);
        });
    }

    /// <summary>
    /// 延时后自动移除通知
    /// </summary>
    private async Task DismissAfterDelayAsync(InfoBar infoBar, TimeSpan duration)
    {
        await Task.Delay(duration);
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (_host?.Children.Contains(infoBar) == true)
            {
                _host.Children.Remove(infoBar);
            }
        });
    }
}
