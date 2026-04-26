// Copyright (c) Helsincy. All rights reserved.

using System.Diagnostics;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Presentation.Shell;
using Launcher.Presentation.Shell.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Serilog;

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 资产浏览页。虚拟化网格 + 缩略图懒加载 + 无限滚动。
/// </summary>
public sealed partial class FabLibraryPage : Page
{
    private static readonly ILogger Logger = Log.ForContext<FabLibraryPage>();

    private bool _hasLoadedOnce;
    private double? _cachedReturnVerticalOffset;

    public FabLibraryViewModel ViewModel { get; }

    public FabLibraryPage()
    {
        ViewModel = ViewModelLocator.Resolve<FabLibraryViewModel>();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        this.InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        if (!_hasLoadedOnce)
        {
            _hasLoadedOnce = true;
            await ViewModel.LoadCommand.ExecuteAsync(null);
            TryRestoreScrollOffsetFromViewModel();
            LogNavigationCacheMetrics("initial_load", startTimestamp);
            return;
        }

        if (_cachedReturnVerticalOffset is double verticalOffset)
        {
            RestoreScrollOffset(verticalOffset);
            _cachedReturnVerticalOffset = null;
        }

        LogNavigationCacheMetrics("cached_return", startTimestamp);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        var verticalOffset = AssetScrollViewer.VerticalOffset;
        ViewModel.SaveCurrentScrollOffset(verticalOffset);
        _cachedReturnVerticalOffset = verticalOffset;
        Logger.Debug(
            "Fab 缓存页已离开可视树，保留页面实例 | VerticalOffset={VerticalOffset} AssetCount={AssetCount}",
            verticalOffset,
            ViewModel.Assets.Count);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is CategoryItem category)
        {
            ViewModel.SelectedCategory = category.Id;
        }
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo)
        {
            ViewModel.SelectedSortIndex = combo.SelectedIndex;
        }
    }

    /// <summary>无限滚动：接近底部时加载更多</summary>
    private async void AssetScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        // 距离底部不到 200px 时触发
        var distanceFromBottom = sv.ScrollableHeight - sv.VerticalOffset;
        if (distanceFromBottom < 200 && ViewModel.HasNextPage && !ViewModel.IsLoadingMore)
        {
            await ViewModel.LoadMoreCommand.ExecuteAsync(null);
        }
    }

    /// <summary>元素进入虚拟化视口时触发缩略图懒加载</summary>
#pragma warning disable CA1822
    private async void AssetGrid_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
#pragma warning restore CA1822
    {
        if (args.Index >= 0 && args.Index < ViewModel.Assets.Count)
        {
            var card = ViewModel.Assets[args.Index];
            await card.LoadThumbnailAsync();
        }
    }

    /// <summary>卡片点击导航到详情页</summary>
    private async void AssetCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FabAssetCardViewModel card)
        {
            ViewModel.SaveCurrentScrollOffset(AssetScrollViewer.VerticalOffset);
            var nav = ViewModelLocator.Resolve<NavigationService>();
            await nav.NavigateAsync(NavigationRoute.FabAssetDetail, card.DetailNavigationPayload);
        }
    }

    private void TryRestoreScrollOffsetFromViewModel()
    {
        if (ViewModel.TryConsumePendingRestoreVerticalOffset(out var verticalOffset))
        {
            RestoreScrollOffset(verticalOffset);
        }
    }

    private void RestoreScrollOffset(double verticalOffset)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AssetScrollViewer.ChangeView(null, verticalOffset, null, true);
        });
    }

    private void LogNavigationCacheMetrics(string outcome, long startTimestamp)
    {
        Logger.Information(
            "Fab 页面缓存实验命中 | Outcome={Outcome} ElapsedMs={ElapsedMs:F1} AssetCount={AssetCount} ManagedMemoryMb={ManagedMemoryMb:F2}",
            outcome,
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            ViewModel.Assets.Count,
            GC.GetTotalMemory(forceFullCollection: false) / 1024d / 1024d);
    }
}

/// <summary>bool → Visibility 转换器</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>bool 取反 → Visibility 转换器</summary>
public sealed class BoolNegationToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
