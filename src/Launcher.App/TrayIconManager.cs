// Copyright (c) Helsincy. All rights reserved.

using System.Drawing;
using Launcher.Shared;
using Serilog;
using WinForms = System.Windows.Forms;

namespace Launcher.App;

/// <summary>
/// 系统托盘图标管理。提供右键菜单和双击激活主窗口功能。
/// </summary>
internal sealed class TrayIconManager : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<TrayIconManager>();
    private WinForms.NotifyIcon? _notifyIcon;

    /// <summary>用户请求显示主窗口</summary>
    public event Action? ShowRequested;

    /// <summary>用户请求退出应用</summary>
    public event Action? ExitRequested;

    /// <summary>
    /// 初始化系统托盘图标和右键菜单
    /// </summary>
    public void Initialize()
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = AppConstants.AppName,
            Icon = SystemIcons.Application,
            Visible = true,
        };

        // 右键菜单
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => ShowRequested?.Invoke());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke());
        _notifyIcon.ContextMenuStrip = menu;

        // 双击激活主窗口
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke();

        Logger.Information("系统托盘图标已创建");
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
            Logger.Debug("系统托盘图标已销毁");
        }
    }
}
