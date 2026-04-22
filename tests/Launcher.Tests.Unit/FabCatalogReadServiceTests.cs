// Copyright (c) Helsincy. All rights reserved.

using System.Net;
using Launcher.Application.Modules.Auth.Contracts;
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
    var fallbackClient = new EpicOwnedFabCatalogClient(factory, authService);
        var sut = new FabCatalogReadService(apiClient, fallbackClient, installReadService);

        var result = await sut.GetDetailAsync("asset-target", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AssetId.Should().Be("asset-target");
        result.Value.Title.Should().Be("Title asset-target");
        result.Value.IsOwned.Should().BeTrue();

        fabHandler.ReceivedRequests.Should().HaveCount(1);
        libraryHandler.ReceivedRequests.Should().HaveCount(1);
    }
}