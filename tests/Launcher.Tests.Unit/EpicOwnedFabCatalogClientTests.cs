// Copyright (c) Helsincy. All rights reserved.

using System.Net;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Infrastructure.FabLibrary;
using Launcher.Shared;
using Launcher.Tests.Unit.Helpers;

namespace Launcher.Tests.Unit;

public sealed class EpicOwnedFabCatalogClientTests
{
    [Fact]
    public async Task SearchOwnedAsync_WhenPageAdvances_ShouldExpandPreviewWindowProgressively()
    {
        var libraryHandler = new MockHttpMessageHandler();
        libraryHandler.EnqueueResponse(
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateLibraryResponse(
                FabOwnedFallbackTestData.CreateOwnedRecordsDescending(count: 20, startIndex: 21),
                nextCursor: "cursor-page-2"));
        libraryHandler.EnqueueResponse(
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateLibraryResponse(
                FabOwnedFallbackTestData.CreateOwnedRecordsDescending(count: 40, startIndex: 1),
                nextCursor: "cursor-page-3"));

        var catalogHandler = new MockHttpMessageHandler();
        FabOwnedFallbackTestData.EnqueueRepeatedResponses(
            catalogHandler,
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateCatalogResponse(Enumerable.Range(1, 40).Select(index => $"asset-{index:D3}")),
            count: 40);

        var sut = CreateSut(libraryHandler, catalogHandler);

        var firstPage = await sut.SearchOwnedAsync(
            new FabSearchQuery { Page = 1, PageSize = 20, SortOrder = FabSortOrder.Newest },
            CancellationToken.None);
        var secondPage = await sut.SearchOwnedAsync(
            new FabSearchQuery { Page = 2, PageSize = 20, SortOrder = FabSortOrder.Newest },
            CancellationToken.None);

        firstPage.IsSuccess.Should().BeTrue();
        firstPage.Value!.Items.Select(item => item.AssetId)
            .Should().Equal(FabOwnedFallbackTestData.CreateExpectedIdsDescending(40, 21));
        firstPage.Value.TotalCount.Should().Be(21);

        secondPage.IsSuccess.Should().BeTrue();
        secondPage.Value!.Items.Select(item => item.AssetId)
            .Should().Equal(FabOwnedFallbackTestData.CreateExpectedIdsDescending(20, 1));
        secondPage.Value.TotalCount.Should().Be(41);

        libraryHandler.ReceivedRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDetailAsync_WhenTargetAssetAppearsInPreview_ShouldReturnMappedDetail()
    {
        var libraryHandler = new MockHttpMessageHandler();
        libraryHandler.EnqueueResponse(
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateLibraryResponse(
            [
                FabOwnedFallbackTestData.CreateOwnedRecord("asset-999", 999),
                FabOwnedFallbackTestData.CreateOwnedRecord("asset-target", 998),
                FabOwnedFallbackTestData.CreateOwnedRecord("asset-888", 997),
            ],
            nextCursor: "cursor-page-2"));

        var catalogHandler = new MockHttpMessageHandler();
        catalogHandler.EnqueueResponse(
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateCatalogResponse(["asset-target"]));

        var sut = CreateSut(libraryHandler, catalogHandler);

        var result = await sut.GetDetailAsync("asset-target", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AssetId.Should().Be("asset-target");
        result.Value.Title.Should().Be("Title asset-target");
        result.Value.Tags.Should().Contain("Environment");
        result.Value.SupportedEngineVersions.Should().Contain("5.4");
        result.Value.TechnicalDetails.Should().Be("Release note asset-target");
        result.Value.Screenshots.Should().Contain("https://cdn.example.com/asset-target/gallery.jpg");

        libraryHandler.ReceivedRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchOwnedAsync_WhenCatalogOmitsImages_ShouldPreservePreviewTraceMetadata()
    {
        var libraryHandler = new MockHttpMessageHandler();
        libraryHandler.EnqueueResponse(
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateLibraryResponse(
            [
                FabOwnedFallbackTestData.CreateOwnedRecord("asset-empty", 1, productId: "product-empty"),
            ]));

        var catalogHandler = new MockHttpMessageHandler();
        catalogHandler.EnqueueResponse(
            HttpStatusCode.OK,
            FabOwnedFallbackTestData.CreateCatalogResponse(
                ["asset-empty"],
                assetIdsWithoutImages: ["asset-empty"],
                listingIdentifiers: new Dictionary<string, string>
                {
                    ["asset-empty"] = "listing-empty",
                }));

        var sut = CreateSut(libraryHandler, catalogHandler);

        var result = await sut.SearchOwnedAsync(
            new FabSearchQuery { Page = 1, PageSize = 20, SortOrder = FabSortOrder.Newest },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle();

        var item = result.Value.Items[0];
        item.AssetId.Should().Be("asset-empty");
        item.ThumbnailUrl.Should().BeEmpty();
        item.PreviewListingId.Should().Be("listing-empty");
        item.PreviewProductId.Should().Be("product-empty");
    }

    private static EpicOwnedFabCatalogClient CreateSut(
        MockHttpMessageHandler libraryHandler,
        MockHttpMessageHandler catalogHandler)
    {
        var authService = Substitute.For<IAuthService>();
        authService.GetAccessTokenAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok("test-access-token"));

        var factory = Substitute.For<IHttpClientFactory>();
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

        return new EpicOwnedFabCatalogClient(factory, authService);
    }
}