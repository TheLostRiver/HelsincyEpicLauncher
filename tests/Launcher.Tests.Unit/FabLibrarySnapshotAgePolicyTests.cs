// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Modules.FabLibrary;

namespace Launcher.Tests.Unit;

public sealed class FabLibrarySnapshotAgePolicyTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-10, (int)FabLibrarySnapshotAgeCategory.Fresh)]
    [InlineData(0, (int)FabLibrarySnapshotAgeCategory.Fresh)]
    [InlineData(30, (int)FabLibrarySnapshotAgeCategory.Fresh)]
    [InlineData(31, (int)FabLibrarySnapshotAgeCategory.Warm)]
    [InlineData(300, (int)FabLibrarySnapshotAgeCategory.Warm)]
    [InlineData(301, (int)FabLibrarySnapshotAgeCategory.Stale)]
    public void Classify_WithExplicitUtcNow_RespectsThresholds(int ageSeconds, int expectedCategory)
    {
        var snapshotAtUtc = FixedUtcNow - TimeSpan.FromSeconds(ageSeconds);

        var category = FabLibrarySnapshotAgePolicy.Classify(snapshotAtUtc, FixedUtcNow);

        category.Should().Be((FabLibrarySnapshotAgeCategory)expectedCategory);
    }

    [Fact]
    public void Classify_WithSnapshot_UsesSnapshotTimestamp()
    {
        var snapshot = new FabLibrarySessionSnapshot
        {
            SnapshotAtUtc = DateTime.UtcNow - TimeSpan.FromMinutes(1),
        };

        var category = FabLibrarySnapshotAgePolicy.Classify(snapshot);

        category.Should().Be(FabLibrarySnapshotAgeCategory.Warm);
    }
}