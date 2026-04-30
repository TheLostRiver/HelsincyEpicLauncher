// Copyright (c) Helsincy. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace Launcher.Presentation.Shell;

/// <summary>
/// 对话框服务实现。使用 WinUI 3 ContentDialog 显示确认、信息和错误对话框。
/// 需在 UI 线程上调用。
/// </summary>
public sealed class DialogService : IDialogService
{
    private static readonly ILogger Logger = Log.ForContext<DialogService>();
    private XamlRoot? _xamlRoot;

    /// <summary>
    /// 设置 XamlRoot。由 ShellPage 在加载时调用，ContentDialog 显示需要此引用。
    /// </summary>
    public void SetXamlRoot(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
        Logger.Debug("DialogService XamlRoot 已设置");
    }

    public async Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "确认", string cancelText = "取消")
    {
        Logger.Information("显示确认对话框 | {Title}", title);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
        };

        var result = await dialog.ShowAsync();
        bool confirmed = result == ContentDialogResult.Primary;

        Logger.Information("确认对话框结果 | {Title} → {Confirmed}", title, confirmed);
        return confirmed;
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        Logger.Information("显示信息对话框 | {Title}", title);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _xamlRoot,
        };

        await dialog.ShowAsync();
    }

    public async Task ShowErrorAsync(string title, string message, bool canRetry = false)
    {
        Logger.Warning("显示错误对话框 | {Title} CanRetry={CanRetry}", title, canRetry);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = canRetry ? "取消" : "确定",
            DefaultButton = canRetry ? ContentDialogButton.Primary : ContentDialogButton.Close,
            XamlRoot = _xamlRoot,
        };

        if (canRetry)
        {
            dialog.PrimaryButtonText = "重试";
        }

        await dialog.ShowAsync();
    }

    public async Task<string?> ShowTextInputAsync(string title, string message, string placeholder = "", string confirmText = "确认", string cancelText = "取消")
    {
        Logger.Information("显示文本输入对话框 | {Title}", title);

        var inputBox = new TextBox
        {
            PlaceholderText = placeholder,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            MinWidth = 360,
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                },
                inputBox,
            },
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? inputBox.Text?.Trim() : null;
    }

    public Task<TResult?> ShowCustomAsync<TResult>(object dialogViewModel)
    {
        // 自定义对话框将在后续任务中实现
        Logger.Warning("ShowCustomAsync 尚未实现");
        return Task.FromResult<TResult?>(default);
    }
}
