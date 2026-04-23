// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Infrastructure.FabLibrary;

internal sealed record FabDetailEnrichmentContext(
    string AssetId,
    string PreviewListingId,
    string PreviewProductId,
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