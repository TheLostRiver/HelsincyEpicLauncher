// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Presentation.Modules.FabLibrary;

namespace Launcher.Tests.Unit;

public sealed class FabLibrarySessionStateStoreTests
{
    private readonly InMemoryFabLibrarySessionStateStore _sut = new();

    [Fact]
    public void Save_ThenTryGet_ReturnsStoredSnapshot()
    {
        var snapshot = CreateSnapshot(assetCount: 2, currentPage: 1, totalPages: 2, totalCount: 2, verticalOffset: 24);

        _sut.Save(snapshot);

        var found = _sut.TryGet(out var restored);

        found.Should().BeTrue();
        restored.Should().NotBeNull();
        restored!.Keyword.Should().Be(snapshot.Keyword);
        restored.Category.Should().Be(snapshot.Category);
        restored.SortOrder.Should().Be(snapshot.SortOrder);
        restored.CurrentPage.Should().Be(snapshot.CurrentPage);
        restored.TotalPages.Should().Be(snapshot.TotalPages);
        restored.TotalCount.Should().Be(snapshot.TotalCount);
        restored.VerticalOffset.Should().Be(snapshot.VerticalOffset);
        restored.AccountScopeKey.Should().Be(snapshot.AccountScopeKey);
        restored.AssetSummaries.Select(item => item.AssetId)
            .Should().ContainInOrder(snapshot.AssetSummaries.Select(item => item.AssetId));
    }

    [Fact]
    public void Save_WhenSnapshotExceedsRetentionLimits_NormalizesStoredSnapshot()
    {
        var snapshot = CreateSnapshot(
            assetCount: FabLibrarySessionSnapshot.MaxRetainedAssetCount + 5,
            currentPage: FabLibrarySessionSnapshot.MaxRetainedPages + 2,
            totalPages: FabLibrarySessionSnapshot.MaxRetainedPages + 4,
            totalCount: FabLibrarySessionSnapshot.MaxRetainedAssetCount + 5,
            hasNextPage: false,
            verticalOffset: 128);

        _sut.Save(snapshot);
        _sut.TryGet(out var restored).Should().BeTrue();

        restored.Should().NotBeNull();
        restored!.AssetSummaries.Should().HaveCount(FabLibrarySessionSnapshot.MaxRetainedAssetCount);
        restored.CurrentPage.Should().Be(FabLibrarySessionSnapshot.MaxRetainedPages);
        restored.HasNextPage.Should().BeTrue();
        restored.VerticalOffset.Should().Be(0);
        restored.AssetSummaries.Select(item => item.AssetId)
            .Should().ContainInOrder(snapshot.AssetSummaries.Take(FabLibrarySessionSnapshot.MaxRetainedAssetCount).Select(item => item.AssetId));
    }

    [Fact]
    public void Clear_RemovesStoredSnapshot()
    {
        _sut.Save(CreateSnapshot(assetCount: 1));

        _sut.Clear();

        var found = _sut.TryGet(out var restored);

        found.Should().BeFalse();
        restored.Should().BeNull();
    }

    [Fact]
    public void Trim_AfterSave_PreservesNormalizedSnapshot()
    {
        _sut.Save(CreateSnapshot(assetCount: 3, currentPage: 2, totalPages: 3, totalCount: 3, verticalOffset: 16));

        _sut.Trim();

        var found = _sut.TryGet(out var restored);

        found.Should().BeTrue();
        restored.Should().NotBeNull();
        restored!.CurrentPage.Should().Be(2);
        restored.AssetSummaries.Should().HaveCount(3);
        restored.VerticalOffset.Should().Be(16);
    }

    private static FabLibrarySessionSnapshot CreateSnapshot(
        int assetCount,
        int currentPage = 1,
        int totalPages = 1,
        int totalCount = 0,
        bool hasNextPage = false,
        double verticalOffset = 0,
        string accountScopeKey = "account-1")
    {
        var summaries = Enumerable.Range(1, assetCount)
            .Select(index => new FabAssetSummary
            {
                AssetId = $"asset-{index}",
                Title = $"Asset {index}",
            })
            .ToArray();

        return new FabLibrarySessionSnapshot
        {
            Keyword = "fab",
            Category = "all",
            SortOrder = FabSortOrder.Newest,
            CurrentPage = currentPage,
            TotalPages = totalPages,
            HasNextPage = hasNextPage,
            TotalCount = totalCount == 0 ? assetCount : totalCount,
            VerticalOffset = verticalOffset,
            SnapshotAtUtc = new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc),
            AccountScopeKey = accountScopeKey,
            AssetSummaries = summaries,
        };
    }
}