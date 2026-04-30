// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Presentation.Shell;

/// <summary>
/// 统一对话框服务。模块不直接弹窗，而是通过此接口请求对话框。
/// </summary>
public interface IDialogService
{
    /// <summary>显示确认对话框，返回用户是否确认</summary>
    Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "确认", string cancelText = "取消");

    /// <summary>显示信息对话框</summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>显示错误对话框</summary>
    Task ShowErrorAsync(string title, string message, bool canRetry = false);

    /// <summary>显示单文本输入对话框</summary>
    Task<string?> ShowTextInputAsync(string title, string message, string placeholder = "", string confirmText = "确认", string cancelText = "取消");

    /// <summary>显示自定义内容对话框（后续任务中实现）</summary>
    Task<TResult?> ShowCustomAsync<TResult>(object dialogViewModel);
}
