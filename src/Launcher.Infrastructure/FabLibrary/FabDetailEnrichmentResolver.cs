// Copyright (c) Helsincy. All rights reserved.

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
    private readonly IFabPreviewMetadataResolver _previewMetadataResolver;

    public FabPreviewBackedDetailEnrichmentResolver(IFabPreviewMetadataResolver previewMetadataResolver)
    {
        _previewMetadataResolver = previewMetadataResolver;
    }

    public async ValueTask<FabDetailEnrichmentResult> TryEnrichAsync(FabDetailEnrichmentContext context, CancellationToken ct)
    {
        if (context.ExistingMediaUrls.Count > 0)
        {
            return FabDetailEnrichmentResult.Empty;
        }

        var mediaUrls = new List<string>();

        if (!string.IsNullOrWhiteSpace(context.PreviewListingId)
            || !string.IsNullOrWhiteSpace(context.PreviewProductId))
        {
            var previewUrl = await _previewMetadataResolver.TryResolveThumbnailUrlAsync(
                new FabPreviewResolutionContext(context.AssetId, context.PreviewListingId, context.PreviewProductId),
                ct);

            if (!string.IsNullOrWhiteSpace(previewUrl))
            {
                mediaUrls.Add(previewUrl);
            }
        }

        if (!string.IsNullOrWhiteSpace(context.ThumbnailUrlHint))
        {
            mediaUrls.Add(context.ThumbnailUrlHint);
        }

        if (mediaUrls.Count == 0)
        {
            return FabDetailEnrichmentResult.Empty;
        }

        return new FabDetailEnrichmentResult
        {
            MediaUrls = mediaUrls
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }
}