// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Infrastructure.FabLibrary;

namespace Launcher.Tests.Unit;

public sealed class FabPreviewUrlReadServiceTests
{
    [Fact]
    public async Task TryResolveThumbnailUrlAsync_WhenHtmlContainsRawPreviewUrl_ShouldReturnFirstUrl()
    {
                var listingPageReadService = new StubFabListingPageReadService(
                [
                        """
                        <html>
                            <body>
                                <img src="https://media.fab.com/image_previews/gallery_images/raw-listing/raw-image.jpg" />
                            </body>
                        </html>
                        """
                ]);
        var resolver = new FabListingHtmlPreviewMetadataResolver(listingPageReadService);
        var sut = new FabPreviewUrlReadService(resolver);

        var result = await sut.TryResolveThumbnailUrlAsync("asset-1", "listing-1", "product-1", CancellationToken.None);

        result.Should().Be("https://media.fab.com/image_previews/gallery_images/raw-listing/raw-image.jpg");
        listingPageReadService.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task TryResolveThumbnailUrlAsync_WhenHtmlContainsEscapedPreviewUrl_ShouldUnescapeAndCache()
    {
        var listingPageReadService = new StubFabListingPageReadService(
        [
            """
            <script>
                window.__STATE__ = {"preview":"https:\/\/media.fab.com\/image_previews\/gallery_images\/escaped-listing\/escaped-image.jpg"};
            </script>
            """
        ]);
        var resolver = new FabListingHtmlPreviewMetadataResolver(listingPageReadService);
        var sut = new FabPreviewUrlReadService(resolver);

        var firstResult = await sut.TryResolveThumbnailUrlAsync("asset-2", "listing-2", "product-2", CancellationToken.None);
        var secondResult = await sut.TryResolveThumbnailUrlAsync("asset-2", "listing-2", "product-2", CancellationToken.None);

        firstResult.Should().Be("https://media.fab.com/image_previews/gallery_images/escaped-listing/escaped-image.jpg");
        secondResult.Should().Be(firstResult);
        listingPageReadService.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task TryResolveThumbnailUrlAsync_WhenListingIdMissing_ShouldFallbackToProductId()
    {
                var listingPageReadService = new StubFabListingPageReadService(
                [
                        """
                        <html>
                            <body>
                                <img src="https://media.fab.com/image_previews/gallery_images/product-fallback/fallback-image.jpg" />
                            </body>
                        </html>
                        """
                ]);
        var resolver = new FabListingHtmlPreviewMetadataResolver(listingPageReadService);
        var sut = new FabPreviewUrlReadService(resolver);

        var result = await sut.TryResolveThumbnailUrlAsync("asset-3", string.Empty, "product-only-listing", CancellationToken.None);

        result.Should().Be("https://media.fab.com/image_previews/gallery_images/product-fallback/fallback-image.jpg");
        listingPageReadService.InvocationCount.Should().Be(1);
        listingPageReadService.RequestedListingIds.Should().ContainSingle().Which.Should().Be("product-only-listing");
    }

    [Fact]
    public async Task TryResolveThumbnailUrlAsync_WhenFirstAttemptReturnsNoPreview_ShouldRetryNextTime()
    {
        var listingPageReadService = new StubFabListingPageReadService(
        [
            "<html><body><div>loading</div></body></html>",
            """
            <html>
              <body>
                <img src="https://media.fab.com/image_previews/gallery_images/retry-listing/retry-image.jpg" />
              </body>
            </html>
            """
        ]);
        var resolver = new FabListingHtmlPreviewMetadataResolver(listingPageReadService);
        var sut = new FabPreviewUrlReadService(resolver);

        var firstResult = await sut.TryResolveThumbnailUrlAsync("asset-4", "retry-listing", string.Empty, CancellationToken.None);
        var secondResult = await sut.TryResolveThumbnailUrlAsync("asset-4", "retry-listing", string.Empty, CancellationToken.None);

        firstResult.Should().BeNull();
        secondResult.Should().Be("https://media.fab.com/image_previews/gallery_images/retry-listing/retry-image.jpg");
        listingPageReadService.InvocationCount.Should().Be(2);
    }

    private sealed class StubFabListingPageReadService(IReadOnlyList<string?> responses) : IFabListingPageReadService
    {
        public int InvocationCount { get; private set; }
        public List<string> RequestedListingIds { get; } = [];

        public Task<string?> TryReadListingHtmlAsync(string listingId, CancellationToken ct)
        {
            InvocationCount++;
            RequestedListingIds.Add(listingId);

            var index = Math.Min(InvocationCount - 1, responses.Count - 1);
            return Task.FromResult(responses[index]);
        }
    }
}