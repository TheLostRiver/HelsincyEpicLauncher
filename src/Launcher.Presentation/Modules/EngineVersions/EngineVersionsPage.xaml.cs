// Copyright (c) Helsincy. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Launcher.Presentation.Modules.EngineVersions;

/// <summary>
/// 引擎版本管理页面。展示可用/已安装版本列表，提供下载、启动、卸载操作。
/// </summary>
public sealed partial class EngineVersionsPage : Page
{
    public EngineVersionsViewModel ViewModel { get; }

    public EngineVersionsPage()
    {
        ViewModel = ViewModelLocator.Resolve<EngineVersionsViewModel>();
        this.InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LoadCommand.CanExecute(null))
            ViewModel.LoadCommand.Execute(null);
    }

#pragma warning disable CA1822 // XAML event handlers must be instance methods
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LoadCommand.CanExecute(null))
            ViewModel.LoadCommand.Execute(null);
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string versionId })
        {
            var item = ViewModel.AvailableVersions.FirstOrDefault(v => v.VersionId == versionId);
            if (item is not null && ViewModel.DownloadCommand.CanExecute(item))
                ViewModel.DownloadCommand.Execute(item);
        }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string versionId })
        {
            var item = ViewModel.InstalledVersions.FirstOrDefault(v => v.VersionId == versionId);
            if (item is not null && ViewModel.LaunchCommand.CanExecute(item))
                ViewModel.LaunchCommand.Execute(item);
        }
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string versionId })
        {
            var item = ViewModel.InstalledVersions.FirstOrDefault(v => v.VersionId == versionId);
            if (item is not null && ViewModel.UninstallCommand.CanExecute(item))
                ViewModel.UninstallCommand.Execute(item);
        }
    }
#pragma warning restore CA1822
}
