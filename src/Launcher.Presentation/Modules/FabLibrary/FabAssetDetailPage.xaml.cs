// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 资产详情页。展示描述、截图画廊、技术细节、下载按钮。
/// </summary>
public sealed partial class FabAssetDetailPage : Page
{
    public FabAssetDetailViewModel ViewModel { get; }

    public FabAssetDetailPage()
    {
        ViewModel = ViewModelLocator.Resolve<FabAssetDetailViewModel>();
        this.InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // AssetId 通过 OnNavigatedTo 导航参数传入
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string assetId && !string.IsNullOrEmpty(assetId))
        {
            await ViewModel.LoadCommand.ExecuteAsync(assetId);
        }
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.GoBackCommand.ExecuteAsync(null);
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DownloadCommand.ExecuteAsync(null);
    }

    private async void RelatedAssetCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string assetId && !string.IsNullOrWhiteSpace(assetId))
        {
            await ViewModel.LoadCommand.ExecuteAsync(assetId);
        }
    }
}
