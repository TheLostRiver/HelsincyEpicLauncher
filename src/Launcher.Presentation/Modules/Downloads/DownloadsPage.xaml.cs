// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Downloads;
using Launcher.Presentation.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Serilog;

namespace Launcher.Presentation.Modules.Downloads;

/// <summary>
/// 下载管理页面。展示下载任务列表、实时进度、暂停/恢复/取消操作。
/// </summary>
public sealed partial class DownloadsPage : Page
{
    private static readonly ILogger Logger = Log.ForContext<DownloadsPage>();

    public DownloadsViewModel ViewModel { get; }

    public DownloadsPage()
    {
        ViewModel = ViewModelLocator.Resolve<DownloadsViewModel>();
        this.InitializeComponent();
        Logger.Debug("DownloadsPage 已创建");
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Dispose();
    }

    private async void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadTaskId taskId })
            await ViewModel.PauseCommand.ExecuteAsync(taskId);
    }

    private async void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadTaskId taskId })
            await ViewModel.ResumeCommand.ExecuteAsync(taskId);
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadTaskId taskId })
            await ViewModel.CancelCommand.ExecuteAsync(taskId);
    }
}

/// <summary>
/// 布尔取反可见性转换器。true → Collapsed, false → Visible。
/// </summary>
public sealed class BoolNegationVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
