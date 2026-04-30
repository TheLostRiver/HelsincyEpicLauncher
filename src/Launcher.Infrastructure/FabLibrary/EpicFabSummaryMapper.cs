// Copyright (c) Helsincy. All rights reserved.

using System.Globalization;
using Launcher.Application.Modules.FabLibrary.Contracts;

namespace Launcher.Infrastructure.FabLibrary;

/// <summary>
/// Pure mapping helpers for Epic catalog payloads used by the owned Fab fallback.
/// </summary>
internal static class EpicFabSummaryMapper
{
    public static FabAssetSummary MapToSummary(OwnedRecord record, EpicCatalogItem? item)
    {
        return new FabAssetSummary
        {
            AssetId = record.CatalogItemId,
            Title = item?.Title ?? record.AppName,
            ThumbnailUrl = SelectThumbnailUrl(item),
            PreviewListingId = ExtractListingIdentifier(item),
            PreviewProductId = record.ProductId,
            Category = SelectCategory(item),
            Author = item?.Developer ?? string.Empty,
            Price = 0,
            Rating = 0,
            IsOwned = true,
            IsInstalled = false,
            SupportedEngineVersions = item?.ReleaseInfo?
                .SelectMany(r => r.CompatibleApps ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [],
        };
    }

    public static List<string> ExtractScreenshotUrls(EpicCatalogItem? item)
    {
        return item?.KeyImages?
            .Select(i => i.Url ?? string.Empty)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    public static List<string> ExtractFormats(EpicCatalogItem? item)
    {
        if (item?.Categories is null || item.Categories.Count == 0)
        {
            return [];
        }

        return item.Categories
            .SelectMany(c => ExtractFormatCandidates(c.Path ?? string.Empty))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ExtractListingIdentifier(EpicCatalogItem? item)
    {
        if (item?.CustomAttributes is null)
        {
            return string.Empty;
        }

        foreach (var pair in item.CustomAttributes)
        {
            if (string.Equals(pair.Key, "ListingIdentifier", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(pair.Value?.Value))
            {
                return pair.Value.Value;
            }
        }

        return string.Empty;
    }

    public static string NormalizeCategory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var candidate = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(candidate)
            || string.Equals(candidate, "type", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate, "asset-format", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate, "format-item", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return NormalizeFacetValue(candidate);
    }

    private static IEnumerable<string> ExtractFormatCandidates(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            yield break;
        }

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!string.Equals(segments[index], "asset-format", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(segments[index], "format-item", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalized = NormalizeFacetValue(segments[^1]);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }

            yield break;
        }
    }

    private static string SelectThumbnailUrl(EpicCatalogItem? item)
    {
        if (item?.KeyImages is null || item.KeyImages.Count == 0)
        {
            return string.Empty;
        }

        var preferred = item.KeyImages.FirstOrDefault(i =>
            !string.IsNullOrWhiteSpace(i.Url)
            && !string.IsNullOrWhiteSpace(i.Type)
            && i.Type.Contains("thumbnail", StringComparison.OrdinalIgnoreCase));

        return preferred?.Url
            ?? item.KeyImages.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Url))?.Url
            ?? string.Empty;
    }

    private static string SelectCategory(EpicCatalogItem? item)
    {
        if (item?.Categories is null)
        {
            return string.Empty;
        }

        foreach (var category in item.Categories)
        {
            var normalized = NormalizeCategory(category.Path ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return string.Empty;
    }

    private static string NormalizeFacetValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            value.Replace('-', ' ').Replace('_', ' '));
    }
}

internal sealed class EpicCatalogItem
{
    public string? Id { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Developer { get; init; }

    public DateTimeOffset LastModifiedDate { get; init; }

    public Dictionary<string, EpicCatalogCustomAttribute> CustomAttributes { get; init; } = [];

    public List<EpicCatalogImage> KeyImages { get; init; } = [];

    public List<EpicCatalogCategory> Categories { get; init; } = [];

    public List<EpicCatalogReleaseInfo> ReleaseInfo { get; init; } = [];
}

internal sealed class EpicCatalogCustomAttribute
{
    public string? Type { get; init; }

    public string? Value { get; init; }
}

internal sealed class EpicCatalogImage
{
    public string? Type { get; init; }

    public string? Url { get; init; }
}

internal sealed class EpicCatalogCategory
{
    public string? Path { get; init; }
}

internal sealed class EpicCatalogReleaseInfo
{
    public DateTimeOffset DateAdded { get; init; }

    public string? VersionTitle { get; init; }

    public string? ReleaseNote { get; init; }

    public List<string> CompatibleApps { get; init; } = [];
}
