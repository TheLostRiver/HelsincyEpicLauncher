// Copyright (c) Helsincy. All rights reserved.

using System.Net;
using System.Text.Json;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Infrastructure.FabLibrary;
using Launcher.Shared;
using Launcher.Tests.Unit.Helpers;

namespace Launcher.Tests.Unit;

public sealed class FabCatalogReadServiceTests
{
    [Fact]
    public async Task GetDetailAsync_WhenFabWebsiteIsChallengeBlocked_ShouldFallbackToEpicOwnedDetail()
    {
        var fabHandler = new MockHttpMessageHandler();
        fabHandler.EnqueueResponse(
            HttpStatusCode.Forbidden,
            "<!DOCTYPE html><html><head><title>Just a moment...</title></head><body>Enable JavaScript and cookies to continue<script>window._cf_chl_opt={};</script></body></html>");

        var libraryHandler = new MockHttpMessageHandler();
        libraryHandler.EnqueueResponse(
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateLibraryResponse(
            [
                FabOwnedFallbackTestData.CreateOwnedRecord("asset-target", 200),
            ],
            nextCursor: "cursor-page-2"));

        var catalogHandler = new MockHttpMessageHandler();
        catalogHandler.EnqueueResponse(
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateCatalogResponse(["asset-target"]));

        var authService = Substitute.For<IAuthService>();
        authService.GetAccessTokenAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok("test-access-token"));

        var installReadService = Substitute.For<IInstallReadService>();
        installReadService.GetInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<InstallStatusSummary>());

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("FabApi")
            .Returns(new HttpClient(fabHandler)
            {
                BaseAddress = new Uri("https://www.fab.com/api"),
            });
        factory.CreateClient("EpicLibraryApi")
            .Returns(new HttpClient(libraryHandler)
            {
                BaseAddress = new Uri("https://library-service.live.use1a.on.epicgames.com"),
            });
        factory.CreateClient("EpicCatalogApi")
            .Returns(new HttpClient(catalogHandler)
            {
                BaseAddress = new Uri("https://catalog-public-service-prod06.ol.epicgames.com"),
            });

        var apiClient = new FabApiClient(factory, authService);
        var fallbackClient = new EpicOwnedFabCatalogClient(factory, authService, new NullFabPreviewMetadataResolver());
        var sut = new FabCatalogReadService(apiClient, fallbackClient, new NullFabDetailEnrichmentResolver(), installReadService);

        var result = await sut.GetDetailAsync("asset-target", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AssetId.Should().Be("asset-target");
        result.Value.Title.Should().Be("Title asset-target");
        result.Value.IsOwned.Should().BeTrue();

        fabHandler.ReceivedRequests.Should().HaveCount(1);
        libraryHandler.ReceivedRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDetailAsync_WhenMainPathHasNoScreenshots_ShouldFallbackToCachedSummaryThumbnail()
    {
        var fabHandler = new MockHttpMessageHandler();
        fabHandler.EnqueueResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                Items = new[]
                {
                    new
                    {
                        AssetId = "asset-target",
                        Title = "Title asset-target",
                        ThumbnailUrl = "https://cdn.example.com/asset-target/thumb.jpg",
                        Category = "Environment",
                        Author = "Epic Test",
                        Price = 0,
                        Rating = 4.6,
                        IsOwned = false,
                        SupportedEngineVersions = new[] { "5.4" },
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 20,
            }, JsonDefaults.SnakeCaseLower));
        fabHandler.EnqueueResponse(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                AssetId = "asset-target",
                Title = "Title asset-target",
                Description = "Description asset-target",
                Author = "Epic Test",
                Price = 0,
                Rating = 4.6,
                RatingCount = 8,
                DownloadSize = 0,
                LatestVersion = "1.0.0",
                UpdatedAt = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                PublishedAt = (DateTime?)null,
                Screenshots = Array.Empty<string>(),
                Formats = Array.Empty<string>(),
                SupportedEngineVersions = new[] { "5.4" },
                Tags = new[] { "Environment" },
                TechnicalDetails = (string?)null,
                IsOwned = false,
            }, JsonDefaults.SnakeCaseLower));

        var authService = Substitute.For<IAuthService>();
        authService.GetAccessTokenAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok("test-access-token"));

        var installReadService = Substitute.For<IInstallReadService>();
        installReadService.GetInstalledAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<InstallStatusSummary>());

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("FabApi")
            .Returns(new HttpClient(fabHandler)
            {
                BaseAddress = new Uri("https://www.fab.com/api"),
            });

        var apiClient = new FabApiClient(factory, authService);
        factory.CreateClient("EpicLibraryApi")
            .Returns(new HttpClient(new MockHttpMessageHandler())
            {
                BaseAddress = new Uri("https://library-service.live.use1a.on.epicgames.com"),
            });
        factory.CreateClient("EpicCatalogApi")
            .Returns(new HttpClient(new MockHttpMessageHandler())
            {
                BaseAddress = new Uri("https://catalog-public-service-prod06.ol.epicgames.com"),
            });

        var fallbackClient = new EpicOwnedFabCatalogClient(factory, authService, new NullFabPreviewMetadataResolver());
        var sut = new FabCatalogReadService(
            apiClient,
            fallbackClient,
            new FabPreviewBackedDetailEnrichmentResolver(new NullFabPreviewMetadataResolver()),
            installReadService);

        var searchResult = await sut.SearchAsync(
            new FabSearchQuery
            {
                Keyword = "target",
                Page = 1,
                PageSize = 20,
                SortOrder = FabSortOrder.Relevance,
            },
            CancellationToken.None);
        var detailResult = await sut.GetDetailAsync("asset-target", CancellationToken.None);

        searchResult.IsSuccess.Should().BeTrue();
        detailResult.IsSuccess.Should().BeTrue();
        detailResult.Value!.Screenshots.Should().ContainSingle();
        detailResult.Value.Screenshots[0].Should().Be("https://cdn.example.com/asset-target/thumb.jpg");
        fabHandler.ReceivedRequests.Should().HaveCount(2);
    }
}