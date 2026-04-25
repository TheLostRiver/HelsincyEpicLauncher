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
    private readonly IFabPreviewUrlReadService _previewUrlReadService;
    private readonly INavigationService _navigationService;
    private readonly IAppConfigProvider _configProvider;
    private readonly DispatcherQueue _dispatcherQueue;
    private string _previewListingId = string.Empty;
    private string _previewProductId = string.Empty;

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
    [ObservableProperty] private string _publishedAtText = string.Empty;
    [ObservableProperty] private string? _technicalDetails;
    [ObservableProperty] private bool _isOwned;
    [ObservableProperty] private bool _isInstalled;

    // === 状态 ===
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private bool _hasScreenshots;
    [ObservableProperty] private bool _hasTechnicalDetails;
    [ObservableProperty] private bool _hasPublishedAt;
    [ObservableProperty] private bool _hasFormats;
    [ObservableProperty] private bool _hasTags;
    [ObservableProperty] private bool _hasSupportedEngineVersions;
    [ObservableProperty] private bool _hasRelatedAssets;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // === 截图 ===
    public ObservableCollection<ScreenshotItem> Screenshots { get; } = [];

    // === 标签 ===
    public ObservableCollection<string> Tags { get; } = [];

    // === 包含格式 ===
    public ObservableCollection<string> Formats { get; } = [];

    // === 兼容引擎版本 ===
    public ObservableCollection<string> SupportedEngineVersions { get; } = [];

    // === 更多内容 ===
    public ObservableCollection<FabRelatedAssetCardViewModel> RelatedAssets { get; } = [];

    // === 缩略图 ===
    [ObservableProperty] private BitmapImage? _heroImage;

    public string DownloadButtonText => IsInstalled ? "已安装" : IsOwned ? "下载" : "获取";
    public bool CanDownload => IsOwned && !IsInstalled && !IsDownloading;
    public string MoreFromAuthorTitle => string.IsNullOrWhiteSpace(Author) ? "更多内容" : $"来自 {Author} 的更多内容";

    partial void OnAuthorChanged(string value)
    {
        OnPropertyChanged(nameof(MoreFromAuthorTitle));
    }

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
        IFabPreviewUrlReadService previewUrlReadService,
        INavigationService navigationService,
        IAppConfigProvider configProvider)
    {
        _catalogService = catalogService;
        _commandService = commandService;
        _thumbnailCache = thumbnailCache;
        _previewUrlReadService = previewUrlReadService;
        _navigationService = navigationService;
        _configProvider = configProvider;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        Logger.Debug("FabAssetDetailViewModel 已创建");
    }

    /// <summary>初始化并加载资产详情</summary>
    [RelayCommand]
    private async Task LoadAsync(string assetId)
    {
        await LoadAsync(new FabAssetDetailNavigationPayload(assetId, string.Empty, string.Empty));
    }

    public async Task LoadAsync(FabAssetDetailNavigationPayload payload)
    {
        if (string.IsNullOrEmpty(payload.AssetId)) return;

        AssetId = payload.AssetId;
        _previewListingId = payload.PreviewListingId;
        _previewProductId = payload.PreviewProductId;
        IsLoading = true;
        HasError = false;
        HeroImage = null;
        RelatedAssets.Clear();
        HasRelatedAssets = false;

        try
        {
            var result = await _catalogService.GetDetailAsync(payload.AssetId, CancellationToken.None);
            if (!result.IsSuccess)
            {
                if (string.Equals(result.Error?.Code, "AUTH_NOT_AUTHENTICATED", StringComparison.Ordinal))
                {
                    HasError = false;
                    ErrorMessage = string.Empty;
                    Logger.Information("Fab 详情当前尚未完成认证，等待会话恢复后自动重载 | AssetId={AssetId}", payload.AssetId);
                    return;
                }

                HasError = true;
                ErrorMessage = result.Error?.UserMessage ?? "加载资产详情失败";
                Logger.Warning("资产详情加载失败 {AssetId}: {Error}", payload.AssetId, result.Error?.TechnicalMessage);
                return;
            }

            var detail = result.Value!;
            UpdateFromDetail(detail);
            await LoadHeroImageAsync(detail.Screenshots);
            await LoadScreenshotsAsync(detail.Screenshots);
            await LoadRelatedAssetsAsync(detail);

            Logger.Information("资产详情已加载 {AssetId}: {Title}", payload.AssetId, detail.Title);
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
        PublishedAtText = detail.PublishedAt?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        TechnicalDetails = detail.TechnicalDetails;
        IsOwned = detail.IsOwned;
        IsInstalled = detail.IsInstalled;
        HasTechnicalDetails = !string.IsNullOrEmpty(detail.TechnicalDetails);
        HasPublishedAt = detail.PublishedAt.HasValue;

        Tags.Clear();
        foreach (var tag in detail.Tags)
            Tags.Add(tag);
        HasTags = Tags.Count > 0;

        Formats.Clear();
        foreach (var format in detail.Formats)
            Formats.Add(format);
        HasFormats = Formats.Count > 0;

        SupportedEngineVersions.Clear();
        foreach (var ver in detail.SupportedEngineVersions)
            SupportedEngineVersions.Add(ver);
        HasSupportedEngineVersions = SupportedEngineVersions.Count > 0;
    }

    private async Task LoadHeroImageAsync(IReadOnlyList<string> screenshots)
    {
        var heroUrl = screenshots.Count > 0
            ? screenshots[0]
            : await TryResolveHeroPreviewUrlAsync();

        if (string.IsNullOrWhiteSpace(heroUrl)) return;

        var localPath = await _thumbnailCache.GetOrDownloadAsync(heroUrl, CancellationToken.None);
        if (localPath is not null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                HeroImage = new BitmapImage(new Uri(localPath));
            });
        }
    }

    private async Task<string?> TryResolveHeroPreviewUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(AssetId)
            || (string.IsNullOrWhiteSpace(_previewListingId) && string.IsNullOrWhiteSpace(_previewProductId)))
        {
            return null;
        }

        return await _previewUrlReadService.TryResolveThumbnailUrlAsync(
            AssetId,
            _previewListingId,
            _previewProductId,
            CancellationToken.None);
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

    private async Task LoadRelatedAssetsAsync(FabAssetDetail detail)
    {
        RelatedAssets.Clear();
        HasRelatedAssets = false;

        if (string.IsNullOrWhiteSpace(detail.Author))
        {
            return;
        }

        try
        {
            var relatedSummaries = await TryLoadRelatedSummariesAsync(detail);
            foreach (var summary in relatedSummaries)
            {
                var card = new FabRelatedAssetCardViewModel(summary, _thumbnailCache, _previewUrlReadService, _dispatcherQueue);
                RelatedAssets.Add(card);
                _ = card.LoadThumbnailAsync();
            }

            HasRelatedAssets = RelatedAssets.Count > 0;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "加载详情页更多内容失败 | AssetId={AssetId}", detail.AssetId);
        }
    }

    private async Task<IReadOnlyList<FabAssetSummary>> TryLoadRelatedSummariesAsync(FabAssetDetail detail)
    {
        var searchResult = await _catalogService.SearchAsync(new FabSearchQuery
        {
            Keyword = detail.Author,
            SortOrder = FabSortOrder.Rating,
            Page = 1,
            PageSize = 12,
        }, CancellationToken.None);

        if (searchResult.IsSuccess)
        {
            var searchMatches = FilterRelatedItems(searchResult.Value!.Items, detail);
            if (searchMatches.Count > 0)
            {
                return searchMatches;
            }
        }

        var ownedResult = await _catalogService.GetOwnedAssetsAsync(CancellationToken.None);
        if (!ownedResult.IsSuccess)
        {
            return [];
        }

        return FilterRelatedItems(ownedResult.Value!, detail);
    }

    private static List<FabAssetSummary> FilterRelatedItems(IEnumerable<FabAssetSummary> items, FabAssetDetail detail)
    {
        var seenAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filtered = new List<FabAssetSummary>();

        foreach (var summary in items)
        {
            if (string.Equals(summary.AssetId, detail.AssetId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(summary.Author, detail.Author, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seenAssetIds.Add(summary.AssetId))
            {
                continue;
            }

            var normalizedTitle = NormalizeRelatedItemTitle(summary.Title);
            if (!string.IsNullOrWhiteSpace(normalizedTitle)
                && !seenTitles.Add(normalizedTitle))
            {
                continue;
            }

            filtered.Add(summary);
            if (filtered.Count == 8)
            {
                break;
            }
        }

        return filtered;
    }

    private static string NormalizeRelatedItemTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            title.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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

/// <summary>详情页底部更多内容卡片</summary>
public partial class FabRelatedAssetCardViewModel : ObservableObject
{
    public string AssetId { get; }
    public string Title { get; }
    public string Author { get; }
    public double Rating { get; }
    public decimal Price { get; }

    [ObservableProperty] private BitmapImage? _thumbnail;
    [ObservableProperty] private bool _isThumbnailLoading = true;
    [ObservableProperty] private bool _showThumbnailPlaceholder;

    private readonly string _thumbnailUrl;
    private readonly string _previewListingId;
    private readonly string _previewProductId;
    private readonly IThumbnailCacheService _thumbnailCache;
    private readonly IFabPreviewUrlReadService _previewUrlReadService;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _thumbnailLoadAttempted;

    public string RatingText => Rating > 0 ? $"★ {Rating:F1}" : string.Empty;
    public string PriceText => Price == 0 ? "免费" : $"${Price:F2}";
    public string ThumbnailMonogram => string.IsNullOrWhiteSpace(Title)
        ? "?"
        : Title.Trim()[0].ToString().ToUpperInvariant();

    public FabRelatedAssetCardViewModel(
        FabAssetSummary summary,
        IThumbnailCacheService thumbnailCache,
        IFabPreviewUrlReadService previewUrlReadService,
        DispatcherQueue dispatcherQueue)
    {
        AssetId = summary.AssetId;
        Title = summary.Title;
        Author = summary.Author;
        Rating = summary.Rating;
        Price = summary.Price;
        _thumbnailUrl = summary.ThumbnailUrl;
        _previewListingId = summary.PreviewListingId;
        _previewProductId = summary.PreviewProductId;
        _thumbnailCache = thumbnailCache;
        _previewUrlReadService = previewUrlReadService;
        _dispatcherQueue = dispatcherQueue;
    }

    public async Task LoadThumbnailAsync()
    {
        if (_thumbnailLoadAttempted)
        {
            IsThumbnailLoading = false;
            return;
        }

        _thumbnailLoadAttempted = true;

        try
        {
            var thumbnailUrl = _thumbnailUrl;
            if (string.IsNullOrWhiteSpace(thumbnailUrl)
                && (!string.IsNullOrWhiteSpace(_previewListingId) || !string.IsNullOrWhiteSpace(_previewProductId)))
            {
                thumbnailUrl = await _previewUrlReadService.TryResolveThumbnailUrlAsync(
                        AssetId,
                        _previewListingId,
                        _previewProductId,
                        CancellationToken.None)
                    ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(thumbnailUrl))
            {
                ShowThumbnailPlaceholder = true;
                IsThumbnailLoading = false;
                return;
            }

            var localPath = await _thumbnailCache.GetOrDownloadAsync(thumbnailUrl, CancellationToken.None);
            if (localPath is null)
            {
                ShowThumbnailPlaceholder = true;
                IsThumbnailLoading = false;
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                Thumbnail = new BitmapImage(new Uri(localPath))
                {
                    DecodePixelWidth = 280,
                    DecodePixelType = DecodePixelType.Logical,
                };
                ShowThumbnailPlaceholder = false;
                IsThumbnailLoading = false;
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "详情页更多内容缩略图加载失败 | AssetId={AssetId}", AssetId);
            _dispatcherQueue.TryEnqueue(() =>
            {
                ShowThumbnailPlaceholder = true;
                IsThumbnailLoading = false;
            });
        }
    }
}
