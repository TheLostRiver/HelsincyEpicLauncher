// Copyright (c) Helsincy. All rights reserved.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Serilog;

namespace Launcher.Infrastructure.FabLibrary;

internal sealed record FabPreviewResolutionContext(string AssetId, string PreviewListingId, string PreviewProductId);

internal interface IFabPreviewMetadataResolver
{
    ValueTask<string?> TryResolveThumbnailUrlAsync(FabPreviewResolutionContext context, CancellationToken ct);
}

internal sealed class FabListingHtmlPreviewMetadataResolver : IFabPreviewMetadataResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private static readonly Regex RawPreviewUrlRegex = new(
        "https://media\\.fab\\.com/image_previews/[^\"'<>\\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EscapedPreviewUrlRegex = new(
        "https:\\\\/\\\\/media\\.fab\\.com\\\\/image_previews\\\\/[^\"'<>\\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IFabListingPageReadService _listingPageReadService;
    private readonly ConcurrentDictionary<string, CachedPreviewResult> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger = Log.ForContext<FabListingHtmlPreviewMetadataResolver>();

    public FabListingHtmlPreviewMetadataResolver(IFabListingPageReadService listingPageReadService)
    {
        _listingPageReadService = listingPageReadService;
    }

    public async ValueTask<string?> TryResolveThumbnailUrlAsync(FabPreviewResolutionContext context, CancellationToken ct)
    {
        var effectiveListingId = !string.IsNullOrWhiteSpace(context.PreviewListingId)
            ? context.PreviewListingId
            : context.PreviewProductId;

        if (string.IsNullOrWhiteSpace(effectiveListingId))
        {
            return null;
        }

        if (_cache.TryGetValue(effectiveListingId, out var cached)
            && DateTime.UtcNow - cached.CachedAt < CacheDuration)
        {
            _logger.Debug("Fab 预览解析命中缓存 | AssetId={AssetId} | ListingId={ListingId} | HasUrl={HasUrl}", context.AssetId, effectiveListingId, !string.IsNullOrWhiteSpace(cached.Url));
            return cached.Url;
        }

        _logger.Debug("Fab 预览解析开始读取 listing 页面 | AssetId={AssetId} | ListingId={ListingId}", context.AssetId, effectiveListingId);
        var html = await _listingPageReadService.TryReadListingHtmlAsync(effectiveListingId, ct);
        var url = ExtractPreviewUrl(html);

        _logger.Information("Fab 预览解析完成 | AssetId={AssetId} | ListingId={ListingId} | UsedProductFallback={UsedProductFallback} | HasHtml={HasHtml} | Resolved={Resolved}",
            context.AssetId,
            effectiveListingId,
            string.IsNullOrWhiteSpace(context.PreviewListingId) && !string.IsNullOrWhiteSpace(context.PreviewProductId),
            !string.IsNullOrWhiteSpace(html),
            !string.IsNullOrWhiteSpace(url));

        if (!string.IsNullOrWhiteSpace(url))
        {
            _cache[effectiveListingId] = new CachedPreviewResult(url, DateTime.UtcNow);
        }
        else
        {
            _cache.TryRemove(effectiveListingId, out _);
        }

        return url;
    }

    internal static string? ExtractPreviewUrl(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var rawMatch = RawPreviewUrlRegex.Match(html);
        if (rawMatch.Success)
        {
            return rawMatch.Value;
        }

        var escapedMatch = EscapedPreviewUrlRegex.Match(html);
        if (!escapedMatch.Success)
        {
            return null;
        }

        return escapedMatch.Value.Replace("\\/", "/", StringComparison.Ordinal);
    }

    private sealed record CachedPreviewResult(string? Url, DateTime CachedAt);
}

internal sealed class NullFabPreviewMetadataResolver : IFabPreviewMetadataResolver
{
    public ValueTask<string?> TryResolveThumbnailUrlAsync(FabPreviewResolutionContext context, CancellationToken ct)
    {
        return ValueTask.FromResult<string?>(null);
    }
}