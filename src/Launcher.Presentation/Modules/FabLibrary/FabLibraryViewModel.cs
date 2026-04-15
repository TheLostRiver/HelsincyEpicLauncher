// Copyright (c) Helsincy. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Application.Modules.Network.Contracts;
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
    private readonly INetworkMonitor _networkMonitor;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource _searchCts = new();
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
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private int _totalCount;

    private const int PageSize = 20;
    private const int SearchDebounceMs = 300;

    public FabLibraryViewModel(
        IFabCatalogReadService catalogService,
        IThumbnailCacheService thumbnailCache,
        INetworkMonitor networkMonitor)
    {
        _catalogService = catalogService;
        _thumbnailCache = thumbnailCache;
        _networkMonitor = networkMonitor;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
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
        try
        {
            // 并行加载分类和首页资产
            var categoriesTask = _catalogService.GetCategoriesAsync(CancellationToken.None);
            var assetsTask = SearchInternalAsync(1);

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

    private async Task SearchInternalAsync(int page, bool append = false)
    {
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
            HasError = true;
            ErrorMessage = result.Error?.UserMessage ?? "加载资产列表失败";
            Logger.Warning("Fab 搜索失败: {Error}", result.Error?.TechnicalMessage);
            return;
        }

        HasError = false;
        var pagedResult = result.Value!;
        UpdatePageState(pagedResult, append);
    }

    private void UpdatePageState(PagedResult<FabAssetSummary> pagedResult, bool append)
    {
        if (!append)
            Assets.Clear();

        foreach (var summary in pagedResult.Items)
        {
            var card = new FabAssetCardViewModel(summary, _thumbnailCache, _dispatcherQueue);
            Assets.Add(card);
        }

        CurrentPage = pagedResult.Page;
        TotalPages = pagedResult.TotalPages;
        HasNextPage = pagedResult.HasNextPage;
        TotalCount = pagedResult.TotalCount;
        HasAssets = Assets.Count > 0;
        IsEmpty = !HasAssets && !IsLoading;
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

    private readonly string _thumbnailUrl;
    private readonly IThumbnailCacheService _thumbnailCache;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _thumbnailLoaded;

    public string PriceText => Price == 0 ? "免费" : $"${Price:F2}";
    public string RatingText => Rating > 0 ? $"★ {Rating:F1}" : string.Empty;

    public FabAssetCardViewModel(FabAssetSummary summary, IThumbnailCacheService thumbnailCache, DispatcherQueue dispatcherQueue)
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
        _thumbnailCache = thumbnailCache;
        _dispatcherQueue = dispatcherQueue;
    }

    /// <summary>
    /// 进入可视区域时触发缩略图加载
    /// </summary>
    public async Task LoadThumbnailAsync()
    {
        if (_thumbnailLoaded || string.IsNullOrEmpty(_thumbnailUrl)) 
        {
            IsThumbnailLoading = false;
            return;
        }

        _thumbnailLoaded = true;

        try
        {
            var localPath = await _thumbnailCache.GetOrDownloadAsync(_thumbnailUrl, CancellationToken.None);
            if (localPath is null)
            {
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
                IsThumbnailLoading = false;
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "缩略图加载失败");
            _dispatcherQueue.TryEnqueue(() => IsThumbnailLoading = false);
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
