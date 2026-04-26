// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Presentation.Modules.FabLibrary;
using Launcher.Shared;

namespace Launcher.Tests.Unit;

public sealed class FabLibraryWarmupCoordinatorTests
{
    private readonly ISettingsReadService _settingsReadService = Substitute.For<ISettingsReadService>();
    private readonly IFabCatalogReadService _catalogReadService = Substitute.For<IFabCatalogReadService>();
    private readonly InMemoryFabLibrarySessionStateStore _sessionStateStore = new();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly INetworkMonitor _networkMonitor = Substitute.For<INetworkMonitor>();

    public FabLibraryWarmupCoordinatorTests()
    {
        _settingsReadService.GetFabLibraryConfig().Returns(new FabLibraryConfig
        {
            AutoWarmOnStartup = true,
        });
        _authService.IsAuthenticated.Returns(true);
        _authService.CurrentUser.Returns(new AuthUserInfo
        {
            AccountId = "account-1",
            DisplayName = "Tester",
        });
        _networkMonitor.IsNetworkAvailable.Returns(true);
    }

    [Fact]
    public async Task WarmAsync_WhenWarmupDisabled_DoesNotSearch()
    {
        _settingsReadService.GetFabLibraryConfig().Returns(new FabLibraryConfig
        {
            AutoWarmOnStartup = false,
        });

        var sut = CreateSut();

        await sut.WarmAsync(CancellationToken.None);

        _ = await _catalogReadService.DidNotReceive().SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WarmAsync_WhenNotAuthenticated_DoesNotSearch()
    {
        _authService.IsAuthenticated.Returns(false);
        _authService.CurrentUser.Returns((AuthUserInfo?)null);

        var sut = CreateSut();

        await sut.WarmAsync(CancellationToken.None);

        _ = await _catalogReadService.DidNotReceive().SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WarmAsync_WhenOffline_DoesNotSearch()
    {
        _networkMonitor.IsNetworkAvailable.Returns(false);

        var sut = CreateSut();

        await sut.WarmAsync(CancellationToken.None);

        _ = await _catalogReadService.DidNotReceive().SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WarmAsync_WhenFreshSnapshotExistsForCurrentAccount_DoesNotSearch()
    {
        _sessionStateStore.Save(new FabLibrarySessionSnapshot
        {
            SnapshotAtUtc = DateTime.UtcNow - TimeSpan.FromSeconds(10),
            AccountScopeKey = "account-1",
            AssetSummaries = [CreateSummary("cached-1")],
        });

        var sut = CreateSut();

        await sut.WarmAsync(CancellationToken.None);

        _ = await _catalogReadService.DidNotReceive().SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WarmAsync_WhenEligible_WritesFirstPageSnapshot()
    {
        _catalogReadService.SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok(new PagedResult<FabAssetSummary>
            {
                Items = [CreateSummary("network-1"), CreateSummary("network-2")],
                Page = 1,
                PageSize = 20,
                TotalCount = 2,
            })));

        var sut = CreateSut();

        await sut.WarmAsync(CancellationToken.None);

        _ = await _catalogReadService.Received(1).SearchAsync(
            Arg.Is<FabSearchQuery>(query =>
                query.Page == 1
                && query.PageSize == 20
                && query.SortOrder == FabSortOrder.Relevance),
            Arg.Any<CancellationToken>());

        _sessionStateStore.TryGet(out var snapshot).Should().BeTrue();
        snapshot.Should().NotBeNull();
        snapshot!.AccountScopeKey.Should().Be("account-1");
        snapshot.AssetSummaries.Select(item => item.AssetId).Should().ContainInOrder("network-1", "network-2");
        snapshot.CurrentPage.Should().Be(1);
        snapshot.TotalCount.Should().Be(2);
    }

    private FabLibraryWarmupCoordinator CreateSut()
    {
        return new FabLibraryWarmupCoordinator(
            _settingsReadService,
            _catalogReadService,
            _sessionStateStore,
            _authService,
            _networkMonitor);
    }

    private static FabAssetSummary CreateSummary(string assetId)
    {
        return new FabAssetSummary
        {
            AssetId = assetId,
            Title = assetId,
        };
    }
}