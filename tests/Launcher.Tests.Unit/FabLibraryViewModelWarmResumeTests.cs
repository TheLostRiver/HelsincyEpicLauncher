// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Launcher.Presentation.Modules.FabLibrary;
using Launcher.Presentation.Shell;
using Launcher.Shared;

namespace Launcher.Tests.Unit;

public sealed class FabLibraryViewModelWarmResumeTests : IDisposable
{
    private readonly IFabCatalogReadService _catalogService = Substitute.For<IFabCatalogReadService>();
    private readonly IThumbnailCacheService _thumbnailCache = Substitute.For<IThumbnailCacheService>();
    private readonly IFabPreviewUrlReadService _previewUrlReadService = Substitute.For<IFabPreviewUrlReadService>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly INetworkMonitor _networkMonitor = Substitute.For<INetworkMonitor>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly InMemoryFabLibrarySessionStateStore _sessionStateStore = new();

    public FabLibraryViewModelWarmResumeTests()
    {
        _authService.IsAuthenticated.Returns(true);
        _authService.CurrentUser.Returns(new AuthUserInfo
        {
            AccountId = "account-1",
            DisplayName = "Tester",
        });
        _networkMonitor.IsNetworkAvailable.Returns(true);
        _catalogService.GetCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok<IReadOnlyList<AssetCategoryInfo>>([])));
    }

    public void Dispose()
    {
    }

    [Fact]
    public async Task LoadAsync_WithoutSnapshot_LoadsFirstPageFromNetwork()
    {
        _catalogService.SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok(CreatePagedResult("network-1", "network-2"))));

        using var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        sut.Assets.Select(asset => asset.AssetId).Should().ContainInOrder("network-1", "network-2");
        sut.IsRestoredFromSnapshot.Should().BeFalse();
        _ = await _catalogService.Received(1).SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>());
        _sessionStateStore.TryGet(out var snapshot).Should().BeTrue();
        snapshot!.AssetSummaries.Select(item => item.AssetId).Should().ContainInOrder("network-1", "network-2");
    }

    [Fact]
    public async Task LoadAsync_WithFreshSnapshot_RestoresSnapshotWithoutNetworkSearch()
    {
        _sessionStateStore.Save(CreateSnapshot("fresh-1", age: TimeSpan.FromSeconds(10)));

        using var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        sut.Assets.Select(asset => asset.AssetId).Should().ContainSingle().Which.Should().Be("fresh-1");
        sut.IsRestoredFromSnapshot.Should().BeTrue();
        sut.ForceNetworkReload.Should().BeFalse();
        _ = await _catalogService.DidNotReceive().SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_WithWarmSnapshot_RestoresThenRefreshesFirstPage()
    {
        var searchTcs = new TaskCompletionSource<Result<PagedResult<FabAssetSummary>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _catalogService.SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => searchTcs.Task);
        _sessionStateStore.Save(CreateSnapshot("warm-1", age: TimeSpan.FromMinutes(1)));

        using var sut = CreateSut();

        var loadTask = sut.LoadCommand.ExecuteAsync(null);
        await Task.Yield();

        sut.Assets.Select(asset => asset.AssetId).Should().ContainSingle().Which.Should().Be("warm-1");
        sut.IsLoading.Should().BeFalse();

        searchTcs.SetResult(Result.Ok(CreatePagedResult("network-1", "network-2")));
        await loadTask;

        sut.Assets.Select(asset => asset.AssetId).Should().ContainInOrder("network-1", "network-2");
        _ = await _catalogService.Received(1).SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>());
        _sessionStateStore.TryGet(out var snapshot).Should().BeTrue();
        snapshot!.AssetSummaries.Select(item => item.AssetId).Should().ContainInOrder("network-1", "network-2");
    }

    [Fact]
    public async Task LoadAsync_WithStaleSnapshot_IgnoresSnapshotAndLoadsFromNetwork()
    {
        _catalogService.SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok(CreatePagedResult("network-1"))));
        _sessionStateStore.Save(CreateSnapshot("stale-1", age: TimeSpan.FromMinutes(10)));

        using var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        sut.Assets.Select(asset => asset.AssetId).Should().ContainSingle().Which.Should().Be("network-1");
        sut.IsRestoredFromSnapshot.Should().BeFalse();
        _ = await _catalogService.Received(1).SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_WithWarmSnapshotRefreshFailure_PreservesSnapshotAndShowsWarning()
    {
        _catalogService.SearchAsync(Arg.Any<FabSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<PagedResult<FabAssetSummary>>(new Error
            {
                Code = "FAB_HTTP_500",
                UserMessage = "刷新失败",
                TechnicalMessage = "server error",
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            })));
        var snapshot = CreateSnapshot("warm-1", age: TimeSpan.FromMinutes(1));
        _sessionStateStore.Save(snapshot);

        using var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        sut.Assets.Select(asset => asset.AssetId).Should().ContainSingle().Which.Should().Be("warm-1");
        sut.HasError.Should().BeFalse();
        _notificationService.Received(1).ShowWarning("刷新失败", Arg.Any<TimeSpan?>());
        _sessionStateStore.TryGet(out var restoredSnapshot).Should().BeTrue();
        restoredSnapshot!.AssetSummaries.Select(item => item.AssetId).Should().ContainSingle().Which.Should().Be("warm-1");
    }

    private FabLibraryViewModel CreateSut()
    {
        return new FabLibraryViewModel(
            _catalogService,
            _thumbnailCache,
            _previewUrlReadService,
            _sessionStateStore,
            _authService,
            _networkMonitor,
            _notificationService,
            action => action());
    }

    private static PagedResult<FabAssetSummary> CreatePagedResult(params string[] assetIds)
    {
        var items = assetIds
            .Select(assetId => CreateSummary(assetId))
            .ToArray();

        return new PagedResult<FabAssetSummary>
        {
            Items = items,
            Page = 1,
            PageSize = 20,
            TotalCount = items.Length,
        };
    }

    private static FabLibrarySessionSnapshot CreateSnapshot(string assetId, TimeSpan age)
    {
        return new FabLibrarySessionSnapshot
        {
            Keyword = string.Empty,
            Category = string.Empty,
            SortOrder = FabSortOrder.Relevance,
            CurrentPage = 1,
            TotalPages = 1,
            HasNextPage = false,
            TotalCount = 1,
            VerticalOffset = 48,
            SnapshotAtUtc = DateTime.UtcNow - age,
            AccountScopeKey = "account-1",
            AssetSummaries = [CreateSummary(assetId)],
        };
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