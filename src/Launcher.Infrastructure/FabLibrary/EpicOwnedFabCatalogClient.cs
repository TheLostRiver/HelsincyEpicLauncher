// Copyright (c) Helsincy. All rights reserved.

using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.FabLibrary;

/// <summary>
/// 基于 Epic 后端 library + catalog 服务的 Fab 已拥有资产回退客户端。
/// 当前仅用于在网页端 Fab API 被 challenge 拦截时恢复“已拥有资产”浏览能力。
/// </summary>
internal sealed class EpicOwnedFabCatalogClient
    : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly IFabPreviewMetadataResolver _previewMetadataResolver;
    private readonly EpicOwnedRecordsClient _ownedRecordsClient;
    private readonly ILogger _logger = Log.ForContext<EpicOwnedFabCatalogClient>();
    private readonly ConcurrentDictionary<string, CachedCatalogItem> _catalogCache = new();

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const int CatalogConcurrency = 8;

    public EpicOwnedFabCatalogClient(
        IHttpClientFactory httpClientFactory,
        IAuthService authService,
        IFabPreviewMetadataResolver previewMetadataResolver)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _previewMetadataResolver = previewMetadataResolver;
        _ownedRecordsClient = new EpicOwnedRecordsClient(httpClientFactory, authService);
    }

    public async Task<Result<PagedResult<FabAssetSummary>>> SearchOwnedAsync(FabSearchQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Max(1, query.PageSize);

        if (RequiresLocalFiltering(query))
        {
            var recordsResult = await GetOwnedRecordsAsync(ct);
            if (!recordsResult.IsSuccess)
            {
                return Result.Fail<PagedResult<FabAssetSummary>>(recordsResult.Error!);
            }

            var filteredRecords = ApplyRecordSort(recordsResult.Value!, query.SortOrder).ToList();
            if (filteredRecords.Count == 0)
            {
                return Result.Ok(new PagedResult<FabAssetSummary>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                });
            }

            var allSummaries = await LoadSummariesAsync(filteredRecords, ct);
            var filtered = ApplySummaryFilters(allSummaries, query).ToList();

            return Result.Ok(new PagedResult<FabAssetSummary>
            {
                Items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = filtered.Count,
                Page = page,
                PageSize = pageSize,
            });
        }

        var windowResult = await GetOwnedRecordWindowAsync(page * pageSize, ct);
        if (!windowResult.IsSuccess)
        {
            return Result.Fail<PagedResult<FabAssetSummary>>(windowResult.Error!);
        }

        var records = ApplyRecordSort(windowResult.Value!.Records, query.SortOrder).ToList();
        if (records.Count == 0)
        {
            return Result.Ok(new PagedResult<FabAssetSummary>
            {
                Items = [],
                TotalCount = 0,
                Page = page,
                PageSize = pageSize,
            });
        }

        var pageRecords = records
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var pageSummaries = await LoadSummariesAsync(pageRecords, ct);
        var totalCount = windowResult.Value.HasMore
            ? Math.Max(records.Count + 1, page * pageSize + 1)
            : records.Count;

        return Result.Ok(new PagedResult<FabAssetSummary>
        {
            Items = pageSummaries,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        });
    }

    public async Task<Result<IReadOnlyList<FabAssetSummary>>> GetOwnedAssetsAsync(CancellationToken ct)
    {
        var recordsResult = await GetOwnedRecordsAsync(ct);
        if (!recordsResult.IsSuccess)
        {
            return Result.Fail<IReadOnlyList<FabAssetSummary>>(recordsResult.Error!);
        }

        var summaries = await LoadSummariesAsync(recordsResult.Value!.ToList(), ct);
        return Result.Ok<IReadOnlyList<FabAssetSummary>>(summaries);
    }

    public Task<Result<IReadOnlyList<AssetCategoryInfo>>> GetCategoriesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _logger.Debug("Epic owned 回退当前不提供分类数据，返回空分类列表");
        return Task.FromResult(Result.Ok<IReadOnlyList<AssetCategoryInfo>>([]));
    }

    public async Task<Result<FabAssetDetail>> GetDetailAsync(string assetId, CancellationToken ct)
    {
        var snapshotResult = await EnsureOwnedRecordsAsync(new OwnedRecordRequirement(RequiredAssetId: assetId), ct);
        if (!snapshotResult.IsSuccess)
        {
            return Result.Fail<FabAssetDetail>(snapshotResult.Error!);
        }

        var record = snapshotResult.Value!.Records
            .FirstOrDefault(r => string.Equals(r.CatalogItemId, assetId, StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            return Result.Fail<FabAssetDetail>(new Error
            {
                Code = "FAB_ASSET_NOT_FOUND",
                UserMessage = "未找到该资产详情",
                TechnicalMessage = $"Owned Fab asset not found for catalogItemId={assetId}",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        var catalogItem = await GetCatalogItemAsync(record, ct);
        var detail = await MapToDetailAsync(record, catalogItem, ct);
        return Result.Ok(detail);
    }

    private Task<Result<IReadOnlyList<OwnedRecord>>> GetOwnedRecordsAsync(CancellationToken ct) =>
        _ownedRecordsClient.GetOwnedRecordsAsync(ct);

    private Task<Result<OwnedRecordWindow>> GetOwnedRecordWindowAsync(int requiredUniqueCount, CancellationToken ct) =>
        _ownedRecordsClient.GetOwnedRecordWindowAsync(requiredUniqueCount, ct);

    private Task<Result<OwnedRecordSnapshot>> EnsureOwnedRecordsAsync(OwnedRecordRequirement requirement, CancellationToken ct) =>
        _ownedRecordsClient.EnsureOwnedRecordsAsync(requirement, ct);

    private async Task<IReadOnlyList<FabAssetSummary>> LoadSummariesAsync(List<OwnedRecord> records, CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var summaries = new FabAssetSummary?[records.Count];
        using var throttler = new SemaphoreSlim(CatalogConcurrency);

        var tasks = records.Select((record, index) => LoadAsync(record, index));
        await Task.WhenAll(tasks);

        return summaries.Where(s => s is not null).Select(s => s!).ToList();

        async Task LoadAsync(OwnedRecord record, int index)
        {
            await throttler.WaitAsync(ct);
            try
            {
                var catalogItem = await GetCatalogItemAsync(record, ct);
                summaries[index] = EpicFabSummaryMapper.MapToSummary(record, catalogItem);
            }
            finally
            {
                throttler.Release();
            }
        }
    }

    private async Task<EpicCatalogItem?> GetCatalogItemAsync(OwnedRecord record, CancellationToken ct)
    {
        if (_catalogCache.TryGetValue(record.CatalogItemId, out var cached)
            && DateTime.UtcNow - cached.CachedAt < CacheDuration)
        {
            return cached.Item;
        }

        try
        {
            var tokenResult = await _authService.GetAccessTokenAsync(ct);
            if (!tokenResult.IsSuccess)
            {
                return null;
            }

            var country = ResolveCountry();
            var locale = ResolveLocale();
            var url = $"/catalog/api/shared/namespace/{Uri.EscapeDataString(record.Namespace)}/bulk/items?id={Uri.EscapeDataString(record.CatalogItemId)}&country={Uri.EscapeDataString(country)}&locale={Uri.EscapeDataString(locale)}";

            var httpClient = _httpClientFactory.CreateClient("EpicCatalogApi");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("读取 Epic catalog 失败 | CatalogItemId={CatalogItemId} | StatusCode={Code}", record.CatalogItemId, response.StatusCode);
                return null;
            }

            var payload = JsonSerializer.Deserialize<Dictionary<string, EpicCatalogItem>>(body, JsonOptions);
            if (payload is null || !payload.TryGetValue(record.CatalogItemId, out var item))
            {
                return null;
            }

            _catalogCache[record.CatalogItemId] = new CachedCatalogItem
            {
                Item = item,
                CachedAt = DateTime.UtcNow,
            };

            return item;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warning(ex, "读取 Epic catalog 详情失败 | CatalogItemId={CatalogItemId}", record.CatalogItemId);
            return null;
        }
    }

    private static IEnumerable<OwnedRecord> ApplyRecordSort(IReadOnlyList<OwnedRecord> records, FabSortOrder sortOrder)
    {
        return sortOrder switch
        {
            FabSortOrder.Newest => records.OrderByDescending(r => r.AcquisitionDate),
            _ => records.OrderByDescending(r => r.AcquisitionDate),
        };
    }

    private static IEnumerable<FabAssetSummary> ApplySummaryFilters(IEnumerable<FabAssetSummary> summaries, FabSearchQuery query)
    {
        var filtered = summaries;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            filtered = filtered.Where(s =>
                s.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || s.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || s.AssetId.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            filtered = filtered.Where(s => string.Equals(s.Category, query.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.EngineVersion))
        {
            filtered = filtered.Where(s => s.SupportedEngineVersions.Any(v => string.Equals(v, query.EngineVersion, StringComparison.OrdinalIgnoreCase)));
        }

        return query.SortOrder switch
        {
            FabSortOrder.Newest => filtered,
            _ => filtered,
        };
    }

    private static bool RequiresLocalFiltering(FabSearchQuery query)
    {
        return !string.IsNullOrWhiteSpace(query.Keyword)
            || !string.IsNullOrWhiteSpace(query.Category)
            || !string.IsNullOrWhiteSpace(query.EngineVersion);
    }

    private async Task<FabAssetDetail> MapToDetailAsync(OwnedRecord record, EpicCatalogItem? item, CancellationToken ct)
    {
        var screenshots = EpicFabSummaryMapper.ExtractScreenshotUrls(item);
        var previewListingId = EpicFabSummaryMapper.ExtractListingIdentifier(item);

        if (screenshots.Count == 0)
        {
            var previewUrl = await _previewMetadataResolver.TryResolveThumbnailUrlAsync(
                new FabPreviewResolutionContext(record.CatalogItemId, previewListingId, record.ProductId),
                ct);

            if (!string.IsNullOrWhiteSpace(previewUrl))
            {
                screenshots = [previewUrl];
            }
        }

        var tags = item?.Categories?
            .Select(c => EpicFabSummaryMapper.NormalizeCategory(c.Path ?? string.Empty))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        var formats = EpicFabSummaryMapper.ExtractFormats(item);

        var supportedVersions = item?.ReleaseInfo?
            .SelectMany(r => r.CompatibleApps ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        var latestRelease = item?.ReleaseInfo?
            .OrderByDescending(r => r.DateAdded)
            .FirstOrDefault();

        DateTime? publishedAt = null;
        if (item?.ReleaseInfo is { Count: > 0 } releaseInfo)
        {
            publishedAt = releaseInfo.Min(r => r.DateAdded.UtcDateTime);
        }

        return new FabAssetDetail
        {
            AssetId = record.CatalogItemId,
            Title = item?.Title ?? record.AppName,
            Description = item?.Description ?? string.Empty,
            Author = item?.Developer ?? string.Empty,
            Price = 0,
            Rating = 0,
            RatingCount = 0,
            DownloadSize = 0,
            LatestVersion = latestRelease?.VersionTitle ?? string.Empty,
            UpdatedAt = item?.LastModifiedDate.UtcDateTime ?? record.AcquisitionDate.UtcDateTime,
            PublishedAt = publishedAt,
            Screenshots = screenshots,
            Formats = formats,
            SupportedEngineVersions = supportedVersions,
            Tags = tags,
            TechnicalDetails = latestRelease?.ReleaseNote,
            IsOwned = true,
            IsInstalled = false,
        };
    }

    private static string ResolveLocale()
    {
        return string.IsNullOrWhiteSpace(CultureInfo.CurrentUICulture.Name)
            ? "en-US"
            : CultureInfo.CurrentUICulture.Name;
    }

    private static string ResolveCountry()
    {
        try
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }
        catch
        {
            return "US";
        }
    }

    private sealed class CachedCatalogItem
    {
        public required EpicCatalogItem Item { get; init; }

        public DateTime CachedAt { get; init; }
    }

    public void Dispose()
    {
        _ownedRecordsClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
