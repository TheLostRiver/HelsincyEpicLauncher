// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Presentation.Shell;

/// <summary>
/// 全局 Toast 通知服务。模块通过此接口显示非阻塞通知。
/// </summary>
public interface INotificationService
{
    /// <summary>显示成功通知</summary>
    void ShowSuccess(string message, TimeSpan? duration = null);

    /// <summary>显示警告通知</summary>
    void ShowWarning(string message, TimeSpan? duration = null);

    /// <summary>显示错误通知</summary>
    void ShowError(string message, TimeSpan? duration = null);

    /// <summary>显示信息通知</summary>
    void ShowInfo(string message, TimeSpan? duration = null);
}
