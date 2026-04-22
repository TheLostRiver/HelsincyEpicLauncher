// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.FabLibrary.Contracts;

namespace Launcher.Infrastructure.FabLibrary;

internal sealed class FabPreviewUrlReadService : IFabPreviewUrlReadService
{
    private readonly IFabPreviewMetadataResolver _previewMetadataResolver;

    public FabPreviewUrlReadService(IFabPreviewMetadataResolver previewMetadataResolver)
    {
        _previewMetadataResolver = previewMetadataResolver;
    }

    public async Task<string?> TryResolveThumbnailUrlAsync(string assetId, string previewListingId, string previewProductId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(previewListingId) && string.IsNullOrWhiteSpace(previewProductId))
        {
            return null;
        }

        return await _previewMetadataResolver.TryResolveThumbnailUrlAsync(
            new FabPreviewResolutionContext(assetId, previewListingId, previewProductId),
            ct);
    }
}