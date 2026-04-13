// Copyright (c) Helsincy. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace Launcher.Presentation.Modules.Diagnostics;

/// <summary>
/// 诊断页面。显示系统信息、磁盘空间、内存使用等诊断数据。
/// </summary>
public sealed partial class DiagnosticsPage : Page
{
    private static readonly ILogger Logger = Log.ForContext<DiagnosticsPage>();

    public DiagnosticsViewModel ViewModel { get; }

    public DiagnosticsPage()
    {
        ViewModel = ViewModelLocator.Resolve<DiagnosticsViewModel>();
        this.InitializeComponent();
        Logger.Debug("DiagnosticsPage 已创建");
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // 页面加载时自动刷新系统信息
        await ViewModel.RefreshSystemInfoCommand.ExecuteAsync(null);
    }
}
