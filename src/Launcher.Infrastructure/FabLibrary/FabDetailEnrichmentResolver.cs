// Copyright (c) Helsincy. All rights reserved.

using System.Globalization;
using System.Text.RegularExpressions;
using Launcher.Application.Modules.FabLibrary.Contracts;

namespace Launcher.Infrastructure.FabLibrary;

internal sealed record FabDetailEnrichmentContext(
    string AssetId,
    string PreviewListingId,
    string PreviewProductId,
    string ThumbnailUrlHint,
    IReadOnlyList<string> ExistingMediaUrls,
    IReadOnlyList<string> ExistingFormats,
    DateTime? ExistingPublishedAt);

internal sealed record FabDetailEnrichmentResult
{
    public static FabDetailEnrichmentResult Empty { get; } = new();

    public IReadOnlyList<string> MediaUrls { get; init; } = [];

    public IReadOnlyList<string> Formats { get; init; } = [];

    public DateTime? PublishedAt { get; init; }

    public bool HasAnyValue => MediaUrls.Count > 0 || Formats.Count > 0 || PublishedAt.HasValue;
}

internal interface IFabDetailEnrichmentResolver
{
    ValueTask<FabDetailEnrichmentResult> TryEnrichAsync(FabDetailEnrichmentContext context, CancellationToken ct);
}

internal sealed class NullFabDetailEnrichmentResolver : IFabDetailEnrichmentResolver
{
    public ValueTask<FabDetailEnrichmentResult> TryEnrichAsync(FabDetailEnrichmentContext context, CancellationToken ct)
    {
        return ValueTask.FromResult(FabDetailEnrichmentResult.Empty);
    }
}

internal sealed class FabPreviewBackedDetailEnrichmentResolver : IFabDetailEnrichmentResolver
{
    private static readonly Regex PublishedAtRegex = new(
        "\"(?:publishedAt|published_at|datePublished|publicationDate)\"\\s*:\\s*\"(?<value>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RawFormatPathRegex = new(
        "(?:asset-format|format-item)/(?<value>[a-z0-9][a-z0-9_-]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EscapedFormatPathRegex = new(
        "(?:asset-format|format-item)\\\\/(?<value>[a-z0-9][a-z0-9_-]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex JsonFormatArrayRegex = new(
        "\"(?:formats|includedFormats|availableFormats)\"\\s*:\\s*\\[(?<values>.*?)\\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex JsonStringValueRegex = new(
        "\"(?<value>[^\"]+)\"",
        RegexOptions.Compiled);

    private readonly IFabPreviewMetadataResolver _previewMetadataResolver;
    private readonly IFabListingPageReadService? _listingPageReadService;

    public FabPreviewBackedDetailEnrichmentResolver(
        IFabPreviewMetadataResolver previewMetadataResolver,
        IFabListingPageReadService? listingPageReadService = null)
    {
        _previewMetadataResolver = previewMetadataResolver;
        _listingPageReadService = listingPageReadService;
    }

    public async ValueTask<FabDetailEnrichmentResult> TryEnrichAsync(FabDetailEnrichmentContext context, CancellationToken ct)
    {
        var needsMedia = context.ExistingMediaUrls.Count == 0;
        var needsFormats = context.ExistingFormats.Count == 0;
        var needsPublishedAt = !context.ExistingPublishedAt.HasValue;

        var listingId = ResolveListingId(context);
        string? listingHtml = null;

        if (_listingPageReadService is not null
            && !string.IsNullOrWhiteSpace(listingId)
            && (needsMedia || needsFormats || needsPublishedAt))
        {
            listingHtml = await _listingPageReadService.TryReadListingHtmlAsync(listingId, ct);
        }

        var mediaUrls = new List<string>();
        if (needsMedia)
        {
            var previewUrl = FabListingHtmlPreviewMetadataResolver.ExtractPreviewUrl(listingHtml);

            if (string.IsNullOrWhiteSpace(previewUrl)
                && (!string.IsNullOrWhiteSpace(context.PreviewListingId)
                    || !string.IsNullOrWhiteSpace(context.PreviewProductId))
                && (_listingPageReadService is null || _previewMetadataResolver is not FabListingHtmlPreviewMetadataResolver))
            {
                previewUrl = await _previewMetadataResolver.TryResolveThumbnailUrlAsync(
                    new FabPreviewResolutionContext(context.AssetId, context.PreviewListingId, context.PreviewProductId),
                    ct);
            }

            if (!string.IsNullOrWhiteSpace(previewUrl))
            {
                mediaUrls.Add(previewUrl);
            }

            if (!string.IsNullOrWhiteSpace(context.ThumbnailUrlHint))
            {
                mediaUrls.Add(context.ThumbnailUrlHint);
            }
        }

        IReadOnlyList<string> formats = [];
        DateTime? publishedAt = null;

        if (!string.IsNullOrWhiteSpace(listingHtml))
        {
            if (needsFormats)
            {
                formats = ExtractFormats(listingHtml);
            }

            if (needsPublishedAt)
            {
                publishedAt = ExtractPublishedAt(listingHtml);
            }
        }

        if (mediaUrls.Count == 0 && formats.Count == 0 && !publishedAt.HasValue)
        {
            return FabDetailEnrichmentResult.Empty;
        }

        return new FabDetailEnrichmentResult
        {
            MediaUrls = mediaUrls
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Formats = formats,
            PublishedAt = publishedAt,
        };
    }

    private static string ResolveListingId(FabDetailEnrichmentContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.PreviewListingId))
        {
            return context.PreviewListingId;
        }

        if (!string.IsNullOrWhiteSpace(context.PreviewProductId))
        {
            return context.PreviewProductId;
        }

        return context.AssetId;
    }

    internal static DateTime? ExtractPublishedAt(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = PublishedAtRegex.Match(html);
        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups["value"].Value;
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    internal static IReadOnlyList<string> ExtractFormats(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ExtractFormatPathMatches(RawFormatPathRegex, html, values);
        ExtractFormatPathMatches(EscapedFormatPathRegex, html, values);
        ExtractJsonFormatArrays(html, values);

        return values.ToList();
    }

    private static void ExtractFormatPathMatches(Regex regex, string html, HashSet<string> values)
    {
        foreach (Match match in regex.Matches(html))
        {
            var normalized = NormalizeFormatValue(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                values.Add(normalized);
            }
        }
    }

    private static void ExtractJsonFormatArrays(string html, HashSet<string> values)
    {
        foreach (Match arrayMatch in JsonFormatArrayRegex.Matches(html))
        {
            var rawArray = arrayMatch.Groups["values"].Value;
            foreach (Match itemMatch in JsonStringValueRegex.Matches(rawArray))
            {
                var normalized = NormalizeFormatValue(itemMatch.Groups["value"].Value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    values.Add(normalized);
                }
            }
        }
    }

    private static string NormalizeFormatValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            raw.Replace('-', ' ').Replace('_', ' '));
    }
}