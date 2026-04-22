// Copyright (c) Helsincy. All rights reserved.

using System.Net;
using System.Text.Json;

namespace Launcher.Tests.Unit.Helpers;

internal sealed record FabOwnedRecordSeed(string Namespace, string CatalogItemId, string AppName, string ProductId, DateTimeOffset AcquisitionDate);

internal static class FabOwnedFallbackTestData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private static readonly string[] CompatibleApps = ["5.4", "5.5"];

    public static IEnumerable<FabOwnedRecordSeed> CreateOwnedRecordsDescending(int count, int startIndex)
    {
        return Enumerable.Range(0, count)
            .Select(offset =>
            {
                var index = startIndex + count - 1 - offset;
                return new FabOwnedRecordSeed(
                    Namespace: "fab-namespace",
                    CatalogItemId: $"asset-{index:D3}",
                    AppName: $"Asset {index:D3}",
                    ProductId: $"product-{index:D3}",
                    AcquisitionDate: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(index));
            });
    }

    public static FabOwnedRecordSeed CreateOwnedRecord(string assetId, int sortIndex, string? productId = null)
    {
        return new FabOwnedRecordSeed(
            Namespace: "fab-namespace",
            CatalogItemId: assetId,
            AppName: $"Asset {assetId}",
            ProductId: productId ?? $"product-{assetId}",
            AcquisitionDate: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(sortIndex));
    }

    public static IReadOnlyList<string> CreateExpectedIdsDescending(int highestInclusive, int lowestInclusive)
    {
        return Enumerable.Range(lowestInclusive, highestInclusive - lowestInclusive + 1)
            .Reverse()
            .Select(index => $"asset-{index:D3}")
            .ToList();
    }

    public static string CreateLibraryResponse(IEnumerable<FabOwnedRecordSeed> records, string? nextCursor = null)
    {
        return JsonSerializer.Serialize(new
        {
            records = records.Select(record => new
            {
                @namespace = record.Namespace,
                catalogItemId = record.CatalogItemId,
                appName = record.AppName,
                productId = record.ProductId,
                sandboxName = "fab-listing-live",
                recordType = "APPLICATION",
                acquisitionDate = record.AcquisitionDate.ToString("O"),
            }),
            responseMetadata = new
            {
                nextCursor,
            },
        }, JsonOptions);
    }

    public static string CreateCatalogResponse(
        IEnumerable<string> assetIds,
        IReadOnlyCollection<string>? assetIdsWithoutImages = null,
        IReadOnlyDictionary<string, string>? listingIdentifiers = null)
    {
        var noImageSet = assetIdsWithoutImages?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var payload = assetIds.ToDictionary(
            assetId => assetId,
            assetId =>
            {
                object customAttributes = new { };
                if (listingIdentifiers is not null && listingIdentifiers.TryGetValue(assetId, out var listingIdentifier))
                {
                    customAttributes = new
                    {
                        ListingIdentifier = new
                        {
                            type = "STRING",
                            value = listingIdentifier,
                        },
                    };
                }

                object[] keyImages = noImageSet.Contains(assetId)
                    ? []
                    :
                    [
                        new
                        {
                            type = "Thumbnail",
                            url = $"https://cdn.example.com/{assetId}/thumb.jpg",
                        },
                        new
                        {
                            type = "Gallery",
                            url = $"https://cdn.example.com/{assetId}/gallery.jpg",
                        },
                    ];

                return new
                {
                    id = assetId,
                    title = $"Title {assetId}",
                    description = $"Description {assetId}",
                    developer = "Epic Test",
                    lastModifiedDate = new DateTimeOffset(2026, 4, 17, 8, 0, 0, TimeSpan.Zero).ToString("O"),
                    customAttributes,
                    keyImages,
                    categories = new[]
                    {
                        new
                        {
                            path = "assets/environment",
                        },
                    },
                    releaseInfo = new[]
                    {
                        new
                        {
                            dateAdded = new DateTimeOffset(2026, 4, 17, 8, 0, 0, TimeSpan.Zero).ToString("O"),
                            versionTitle = "5.4.0",
                            releaseNote = $"Release note {assetId}",
                            compatibleApps = CompatibleApps,
                        },
                    },
                };
            });

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static void EnqueueRepeatedResponses(MockHttpMessageHandler handler, HttpStatusCode statusCode, string body, int count)
    {
        for (var index = 0; index < count; index++)
        {
            handler.EnqueueResponse(statusCode, body);
        }
    }
}