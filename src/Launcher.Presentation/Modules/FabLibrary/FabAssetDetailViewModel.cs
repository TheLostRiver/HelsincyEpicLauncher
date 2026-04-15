// Copyright (c) Helsincy. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Presentation.Shell.Navigation;
using Launcher.Shared.Configuration;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 资产详情页 ViewModel。加载资产详情、截图画廊、发起下载。
/// </summary>
public partial class FabAssetDetailViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<FabAssetDetailViewModel>();

    private readonly IFabCatalogReadService _catalogService;
    private readonly IFabAssetCommandService _commandService;
    private readonly IThumbnailCacheService _thumbnailCache;
    private readonly INavigationService _navigationService;
    private readonly IAppConfigProvider _configProvider;
    private readonly DispatcherQueue _dispatcherQueue;

    // === 基础信息 ===
    [ObservableProperty] private string _assetId = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _priceText = string.Empty;
    [ObservableProperty] private string _ratingText = string.Empty;
    [ObservableProperty] private int _ratingCount;
    [ObservableProperty] private string _downloadSizeText = string.Empty;
    [ObservableProperty] private string _latestVersion = string.Empty;
    [ObservableProperty] private string _updatedAtText = string.Empty;
    [ObservableProperty] private string? _technicalDetails;
    [ObservableProperty] private bool _isOwned;
    [ObservableProperty] private bool _isInstalled;

    // === 状态 ===
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private bool _hasScreenshots;
    [ObservableProperty] private bool _hasTechnicalDetails;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // === 截图 ===
    public ObservableCollection<ScreenshotItem> Screenshots { get; } = [];

    // === 标签 ===
    public ObservableCollection<string> Tags { get; } = [];

    // === 兼容引擎版本 ===
    public ObservableCollection<string> SupportedEngineVersions { get; } = [];

    // === 缩略图 ===
    [ObservableProperty] private BitmapImage? _heroImage;

    public string DownloadButtonText => IsInstalled ? "已安装" : IsOwned ? "下载" : "获取";
    public bool CanDownload => IsOwned && !IsInstalled && !IsDownloading;

    partial void OnIsOwnedChanged(bool value)
    {
        OnPropertyChanged(nameof(DownloadButtonText));
        OnPropertyChanged(nameof(CanDownload));
    }

    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(DownloadButtonText));
        OnPropertyChanged(nameof(CanDownload));
    }

    partial void OnIsDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDownload));
    }

    public FabAssetDetailViewModel(
        IFabCatalogReadService catalogService,
        IFabAssetCommandService commandService,
        IThumbnailCacheService thumbnailCache,
        INavigationService navigationService,
        IAppConfigProvider configProvider)
    {
        _catalogService = catalogService;
        _commandService = commandService;
        _thumbnailCache = thumbnailCache;
        _navigationService = navigationService;
        _configProvider = configProvider;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        Logger.Debug("FabAssetDetailViewModel 已创建");
    }

    /// <summary>初始化并加载资产详情</summary>
    [RelayCommand]
    private async Task LoadAsync(string assetId)
    {
        if (string.IsNullOrEmpty(assetId)) return;

        AssetId = assetId;
        IsLoading = true;
        HasError = false;

        try
        {
            var result = await _catalogService.GetDetailAsync(assetId, CancellationToken.None);
            if (!result.IsSuccess)
            {
                HasError = true;
                ErrorMessage = result.Error?.UserMessage ?? "加载资产详情失败";
                Logger.Warning("资产详情加载失败 {AssetId}: {Error}", assetId, result.Error?.TechnicalMessage);
                return;
            }

            var detail = result.Value!;
            UpdateFromDetail(detail);
            await LoadHeroImageAsync(detail.Screenshots);
            await LoadScreenshotsAsync(detail.Screenshots);

            Logger.Information("资产详情已加载 {AssetId}: {Title}", assetId, detail.Title);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>发起下载</summary>
    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (!CanDownload) return;

        IsDownloading = true;
        try
        {
            // 默认安装路径
            var installPath = Path.Combine(
                _configProvider.InstallPath, "Assets", AssetId);

            var result = await _commandService.DownloadAssetAsync(AssetId, installPath, CancellationToken.None);
            if (result.IsSuccess)
            {
                Logger.Information("资产下载已发起 {AssetId}, TaskId={TaskId}", AssetId, result.Value);
            }
            else
            {
                Logger.Warning("资产下载失败 {AssetId}: {Error}", AssetId, result.Error?.TechnicalMessage);
            }
        }
        finally
        {
            IsDownloading = false;
        }
    }

    /// <summary>返回上一页</summary>
    [RelayCommand]
    private async Task GoBackAsync()
    {
        await _navigationService.GoBackAsync();
    }

    private void UpdateFromDetail(FabAssetDetail detail)
    {
        Title = detail.Title;
        Description = detail.Description;
        Author = detail.Author;
        PriceText = detail.Price == 0 ? "免费" : $"${detail.Price:F2}";
        RatingText = detail.Rating > 0 ? $"★ {detail.Rating:F1}" : "暂无评分";
        RatingCount = detail.RatingCount;
        DownloadSizeText = FormatSize(detail.DownloadSize);
        LatestVersion = detail.LatestVersion;
        UpdatedAtText = detail.UpdatedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        TechnicalDetails = detail.TechnicalDetails;
        IsOwned = detail.IsOwned;
        IsInstalled = detail.IsInstalled;
        HasTechnicalDetails = !string.IsNullOrEmpty(detail.TechnicalDetails);

        Tags.Clear();
        foreach (var tag in detail.Tags)
            Tags.Add(tag);

        SupportedEngineVersions.Clear();
        foreach (var ver in detail.SupportedEngineVersions)
            SupportedEngineVersions.Add(ver);
    }

    private async Task LoadHeroImageAsync(IReadOnlyList<string> screenshots)
    {
        if (screenshots.Count == 0) return;

        var localPath = await _thumbnailCache.GetOrDownloadAsync(screenshots[0], CancellationToken.None);
        if (localPath is not null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                HeroImage = new BitmapImage(new Uri(localPath));
            });
        }
    }

    private Task LoadScreenshotsAsync(IReadOnlyList<string> screenshotUrls)
    {
        Screenshots.Clear();
        HasScreenshots = screenshotUrls.Count > 0;

        foreach (var url in screenshotUrls)
        {
            var item = new ScreenshotItem(url);
            Screenshots.Add(item);

            // 异步加载缩略图
            _ = LoadScreenshotImageAsync(item);
        }

        return Task.CompletedTask;
    }

    private async Task LoadScreenshotImageAsync(ScreenshotItem item)
    {
        var localPath = await _thumbnailCache.GetOrDownloadAsync(item.Url, CancellationToken.None);
        if (localPath is not null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                item.Image = new BitmapImage(new Uri(localPath));
            });
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "未知";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

/// <summary>截图项</summary>
public partial class ScreenshotItem : ObservableObject
{
    public string Url { get; }

    [ObservableProperty] private BitmapImage? _image;

    public ScreenshotItem(string url) => Url = url;
}
