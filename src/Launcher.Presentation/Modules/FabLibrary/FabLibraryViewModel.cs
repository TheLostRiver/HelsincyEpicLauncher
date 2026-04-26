// Copyright (c) Helsincy. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Launcher.Presentation.Shell;
using Launcher.Shared;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 资产浏览页 ViewModel。支持分页/无限滚动、搜索防抖、缩略图懒加载。
/// </summary>
public partial class FabLibraryViewModel : ObservableObject, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<FabLibraryViewModel>();

    private readonly IFabCatalogReadService _catalogService;
    private readonly IThumbnailCacheService _thumbnailCache;
    private readonly IFabPreviewUrlReadService _previewUrlReadService;
    private readonly IFabLibrarySessionStateStore _sessionStateStore;
    private readonly INetworkMonitor _networkMonitor;
    private readonly INotificationService _notificationService;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource _searchCts = new();
    private bool _isRestoredFromSnapshot;
    private FabLibrarySnapshotAgeCategory? _restoredSnapshotAgeCategory;
    private bool _forceNetworkReload;
    private double? _pendingRestoreVerticalOffset;
    private bool _disposed;

    /// <summary>资产卡片列表</summary>
    public ObservableCollection<FabAssetCardViewModel> Assets { get; } = [];

    /// <summary>分类列表</summary>
    public ObservableCollection<CategoryItem> Categories { get; } = [];

    /// <summary>骨架屏占位数据</summary>
    public int[] SkeletonItems { get; } = [1, 2, 3, 4, 5, 6, 7, 8];

    // === 状态属性 ===
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private bool _hasAssets;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isOffline;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private string _selectedCategory = string.Empty;
    [ObservableProperty] private FabSortOrder _selectedSortOrder = FabSortOrder.Relevance;
    [ObservableProperty] private int _selectedSortIndex;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private int _totalCount;

    internal bool IsRestoredFromSnapshot => _isRestoredFromSnapshot;

    internal bool ForceNetworkReload => _forceNetworkReload;

    private const int PageSize = 20;
    private const int SearchDebounceMs = 300;

    public FabLibraryViewModel(
        IFabCatalogReadService catalogService,
        IThumbnailCacheService thumbnailCache,
        IFabPreviewUrlReadService previewUrlReadService,
        IFabLibrarySessionStateStore sessionStateStore,
        INetworkMonitor networkMonitor,
        INotificationService notificationService)
    {
        _catalogService = catalogService;
        _thumbnailCache = thumbnailCache;
        _previewUrlReadService = previewUrlReadService;
        _sessionStateStore = sessionStateStore;
        _networkMonitor = networkMonitor;
        _notificationService = notificationService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _isRestoredFromSnapshot = false;
        _restoredSnapshotAgeCategory = null;
        _forceNetworkReload = false;
        _pendingRestoreVerticalOffset = null;
        _isOffline = !networkMonitor.IsNetworkAvailable;
        _networkMonitor.NetworkStatusChanged += OnNetworkStatusChanged;

        Logger.Debug("FabLibraryViewModel 已创建");
    }

    /// <summary>页面加载时初始化</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        HasError = false;
        IsLoading = true;
        var restoredFromSnapshot = TryRestorePageStateFromSnapshot();
        try
        {
            // 并行加载分类和首页资产
            var categoriesTask = _catalogService.GetCategoriesAsync(CancellationToken.None);
            var assetsTask = restoredFromSnapshot
                ? _forceNetworkReload
                    ? SearchInternalAsync(1, preserveVisibleContentOnFailure: true)
                    : Task.CompletedTask
                : SearchInternalAsync(1);

            if (restoredFromSnapshot && _restoredSnapshotAgeCategory is not FabLibrarySnapshotAgeCategory.Stale)
            {
                IsLoading = false;
            }

            if (restoredFromSnapshot && _forceNetworkReload)
            {
                Logger.Information("Fab Warm 快照已恢复，开始静默刷新第一页");
            }

            await categoriesTask;
            var categoriesResult = categoriesTask.Result;
            if (categoriesResult.IsSuccess)
            {
                Categories.Clear();
                Categories.Add(new CategoryItem { Id = string.Empty, Name = "全部", AssetCount = 0 });
                foreach (var cat in categoriesResult.Value!)
                    Categories.Add(new CategoryItem { Id = cat.Id, Name = cat.Name, AssetCount = cat.AssetCount });
            }

            await assetsTask;
            Logger.Information("Fab 页面初始化完成：{Count} 个资产，{Categories} 个分类",
                Assets.Count, Categories.Count);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>搜索关键词变更（防抖 300ms）</summary>
    partial void OnSearchKeywordChanged(string value)
    {
        _ = SearchWithDebounceAsync();
    }

    /// <summary>分类切换</summary>
    partial void OnSelectedCategoryChanged(string value)
    {
        _ = SearchWithDebounceAsync();
    }

    /// <summary>排序索引切换：映射 ComboBox 索引到 FabSortOrder</summary>
    partial void OnSelectedSortIndexChanged(int value)
    {
        SelectedSortOrder = value switch
        {
            0 => FabSortOrder.Relevance,
            1 => FabSortOrder.Newest,
            2 => FabSortOrder.Rating,
            3 => FabSortOrder.PriceLowToHigh,
            4 => FabSortOrder.PriceHighToLow,
            _ => FabSortOrder.Relevance,
        };
    }

    /// <summary>排序切换</summary>
    partial void OnSelectedSortOrderChanged(FabSortOrder value)
    {
        _ = SearchWithDebounceAsync();
    }

    /// <summary>加载下一页（无限滚动触发）</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoadingMore || !HasNextPage) return;

        IsLoadingMore = true;
        try
        {
            await SearchInternalAsync(CurrentPage + 1, append: true);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <summary>手动刷新</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await SearchInternalAsync(1);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchWithDebounceAsync()
    {
        // 取消之前的搜索
        await CancelPendingSearchAsync();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            await Task.Delay(SearchDebounceMs, ct);
            if (ct.IsCancellationRequested) return;

            IsLoading = true;
            await SearchInternalAsync(1);
        }
        catch (TaskCanceledException)
        {
            // 被新搜索取消，正常
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchInternalAsync(int page, bool append = false, bool preserveVisibleContentOnFailure = false)
    {
        var hadVisibleAssets = Assets.Count > 0;
        var shouldPreserveVisibleContent = preserveVisibleContentOnFailure && hadVisibleAssets && !append;
        var query = new FabSearchQuery
        {
            Keyword = string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword.Trim(),
            Category = string.IsNullOrWhiteSpace(SelectedCategory) ? null : SelectedCategory,
            SortOrder = SelectedSortOrder,
            Page = page,
            PageSize = PageSize,
        };

        var result = await _catalogService.SearchAsync(query, CancellationToken.None);
        if (!result.IsSuccess)
        {
            if (string.Equals(result.Error?.Code, "AUTH_NOT_AUTHENTICATED", StringComparison.Ordinal))
            {
                ClearPageError();

                if (!append && !shouldPreserveVisibleContent)
                {
                    Assets.Clear();
                    CurrentPage = 1;
                    TotalPages = 0;
                    HasNextPage = false;
                    TotalCount = 0;
                    HasAssets = false;
                    IsEmpty = false;
                }

                if (shouldPreserveVisibleContent)
                {
                    var authUserMessage = result.Error?.UserMessage ?? "当前登录状态已失效";
                    _notificationService.ShowWarning(authUserMessage);
                    Logger.Warning("Fab 静默刷新失败（认证失效，保留当前列表）: {Error}", result.Error?.TechnicalMessage);
                    return;
                }

                Logger.Information("Fab 当前尚未完成认证，等待会话恢复后自动重载");
                return;
            }

            var userMessage = result.Error?.UserMessage ?? "加载资产列表失败";
            if (append || hadVisibleAssets)
            {
                ClearPageError();
                _notificationService.ShowWarning(userMessage);
                Logger.Warning("Fab 搜索失败（非阻断）: {Error}", result.Error?.TechnicalMessage);
                return;
            }

            HasError = true;
            ErrorMessage = userMessage;
            Logger.Warning("Fab 搜索失败（阻断）: {Error}", result.Error?.TechnicalMessage);
            return;
        }

        ClearPageError();
        var pagedResult = result.Value!;
        UpdatePageState(pagedResult, append);
        SaveSessionSnapshot();
    }

    private void ClearPageError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private void UpdatePageState(PagedResult<FabAssetSummary> pagedResult, bool append)
    {
        if (!append)
            Assets.Clear();

        foreach (var summary in pagedResult.Items)
        {
            var card = new FabAssetCardViewModel(summary, _thumbnailCache, _previewUrlReadService, _dispatcherQueue);
            Assets.Add(card);
        }

        CurrentPage = pagedResult.Page;
        TotalPages = pagedResult.TotalPages;
        HasNextPage = pagedResult.HasNextPage;
        TotalCount = pagedResult.TotalCount;
        HasAssets = Assets.Count > 0;
        IsEmpty = !HasAssets && !IsLoading;
    }

    private bool TryRestorePageStateFromSnapshot()
    {
        if (!_sessionStateStore.TryGet(out var snapshot) || snapshot is null)
        {
            _isRestoredFromSnapshot = false;
            _restoredSnapshotAgeCategory = null;
            _forceNetworkReload = false;
            return false;
        }

        var ageCategory = FabLibrarySnapshotAgePolicy.Classify(snapshot);
        _restoredSnapshotAgeCategory = ageCategory;
        RestorePageState(snapshot);
        _forceNetworkReload = ageCategory is FabLibrarySnapshotAgeCategory.Warm;
        ClearPageError();
        Logger.Information(
            "Fab 页面已从会话快照恢复 | Count={Count} Page={Page} AgeCategory={AgeCategory} ForceReload={ForceReload}",
            Assets.Count,
            CurrentPage,
            ageCategory,
            _forceNetworkReload);
        return true;
    }

    private void RestorePageState(FabLibrarySessionSnapshot snapshot)
    {
        Assets.Clear();

        foreach (var summary in snapshot.AssetSummaries)
        {
            var card = new FabAssetCardViewModel(summary, _thumbnailCache, _previewUrlReadService, _dispatcherQueue);
            Assets.Add(card);
        }

        CurrentPage = snapshot.CurrentPage;
        TotalPages = snapshot.TotalPages;
        HasNextPage = snapshot.HasNextPage;
        TotalCount = snapshot.TotalCount;
        HasAssets = Assets.Count > 0;
        IsEmpty = !HasAssets;
        _isRestoredFromSnapshot = HasAssets;
        _pendingRestoreVerticalOffset = snapshot.VerticalOffset;
    }

    private void SaveSessionSnapshot(double verticalOffset = 0)
    {
        var assetSummaries = new List<FabAssetSummary>(Assets.Count);
        foreach (var card in Assets)
        {
            assetSummaries.Add(card.ToSummary());
        }

        var snapshot = new FabLibrarySessionSnapshot
        {
            Keyword = SearchKeyword,
            Category = SelectedCategory,
            SortOrder = SelectedSortOrder,
            CurrentPage = CurrentPage,
            TotalPages = TotalPages,
            HasNextPage = HasNextPage,
            TotalCount = TotalCount,
            VerticalOffset = verticalOffset,
            SnapshotAtUtc = DateTime.UtcNow,
            AccountScopeKey = string.Empty,
            AssetSummaries = assetSummaries,
        };

        _sessionStateStore.Save(snapshot);
        _isRestoredFromSnapshot = false;
        _restoredSnapshotAgeCategory = null;
        _forceNetworkReload = false;
    }

    internal void SaveCurrentScrollOffset(double verticalOffset)
    {
        SaveSessionSnapshot(verticalOffset);
    }

    internal bool TryConsumePendingRestoreVerticalOffset(out double verticalOffset)
    {
        if (_pendingRestoreVerticalOffset is not double offset || offset <= 0)
        {
            verticalOffset = 0;
            _pendingRestoreVerticalOffset = null;
            return false;
        }

        verticalOffset = offset;
        _pendingRestoreVerticalOffset = null;
        return true;
    }

    private async Task CancelPendingSearchAsync()
    {
        try
        {
            await _searchCts.CancelAsync();
            _searchCts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // 忽略
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _networkMonitor.NetworkStatusChanged -= OnNetworkStatusChanged;
        _searchCts.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnNetworkStatusChanged(bool isAvailable)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsOffline = !isAvailable;
            Logger.Debug("FabLibraryViewModel 网络状态变化 | IsOffline={IsOffline}", IsOffline);
        });
    }
}

/// <summary>
/// 单个 Fab 资产卡片 ViewModel。支持缩略图懒加载。
/// </summary>
public partial class FabAssetCardViewModel : ObservableObject
{
    public string AssetId { get; }
    public string Title { get; }
    public string Category { get; }
    public string Author { get; }
    public decimal Price { get; }
    public double Rating { get; }
    public bool IsOwned { get; }
    public bool IsInstalled { get; }

    [ObservableProperty] private BitmapImage? _thumbnail;
    [ObservableProperty] private bool _isThumbnailLoading = true;
    [ObservableProperty] private bool _showThumbnailPlaceholder;

    private readonly string _thumbnailUrl;
    private readonly string _previewListingId;
    private readonly string _previewProductId;
    private readonly IReadOnlyList<string> _supportedEngineVersions;
    private readonly IThumbnailCacheService _thumbnailCache;
    private readonly IFabPreviewUrlReadService _previewUrlReadService;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _thumbnailLoadAttempted;

    public string PriceText => Price == 0 ? "免费" : $"${Price:F2}";
    public string RatingText => Rating > 0 ? $"★ {Rating:F1}" : string.Empty;
    public string ThumbnailMonogram => string.IsNullOrWhiteSpace(Title)
        ? "?"
        : Title.Trim()[0].ToString().ToUpperInvariant();
    public string ThumbnailStatusText => HasPreviewLocator ? "平台未返回预览" : "暂无预览";
    public FabAssetDetailNavigationPayload DetailNavigationPayload => new(AssetId, _previewListingId, _previewProductId);

    private bool HasPreviewLocator => !string.IsNullOrWhiteSpace(_previewListingId) || !string.IsNullOrWhiteSpace(_previewProductId);

    public FabAssetCardViewModel(
        FabAssetSummary summary,
        IThumbnailCacheService thumbnailCache,
        IFabPreviewUrlReadService previewUrlReadService,
        DispatcherQueue dispatcherQueue)
    {
        AssetId = summary.AssetId;
        Title = summary.Title;
        Category = summary.Category;
        Author = summary.Author;
        Price = summary.Price;
        Rating = summary.Rating;
        IsOwned = summary.IsOwned;
        IsInstalled = summary.IsInstalled;
        _thumbnailUrl = summary.ThumbnailUrl;
        _previewListingId = summary.PreviewListingId;
        _previewProductId = summary.PreviewProductId;
        _supportedEngineVersions = summary.SupportedEngineVersions;
        _thumbnailCache = thumbnailCache;
        _previewUrlReadService = previewUrlReadService;
        _dispatcherQueue = dispatcherQueue;
    }

    public FabAssetSummary ToSummary()
    {
        return new FabAssetSummary
        {
            AssetId = AssetId,
            Title = Title,
            ThumbnailUrl = _thumbnailUrl,
            PreviewListingId = _previewListingId,
            PreviewProductId = _previewProductId,
            Category = Category,
            Author = Author,
            Price = Price,
            Rating = Rating,
            IsOwned = IsOwned,
            IsInstalled = IsInstalled,
            SupportedEngineVersions = _supportedEngineVersions,
        };
    }

    /// <summary>
    /// 进入可视区域时触发缩略图加载
    /// </summary>
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
            if (string.IsNullOrWhiteSpace(thumbnailUrl) && HasPreviewLocator)
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
                // DecodePixelWidth 限制解码尺寸，减少内存占用 + 提升滚动帧率
                Thumbnail = new BitmapImage(new Uri(localPath))
                {
                    DecodePixelWidth = 220,
                    DecodePixelType = DecodePixelType.Logical
                };
                ShowThumbnailPlaceholder = false;
                IsThumbnailLoading = false;
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "缩略图加载失败");
            _dispatcherQueue.TryEnqueue(() =>
            {
                ShowThumbnailPlaceholder = true;
                IsThumbnailLoading = false;
            });
        }
    }
}

/// <summary>分类选择项</summary>
public sealed class CategoryItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int AssetCount { get; init; }
    public override string ToString() => AssetCount > 0 ? $"{Name} ({AssetCount})" : Name;
}
