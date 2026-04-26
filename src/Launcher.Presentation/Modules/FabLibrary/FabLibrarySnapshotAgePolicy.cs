// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Presentation.Modules.FabLibrary;

internal enum FabLibrarySnapshotAgeCategory
{
    Fresh,
    Warm,
    Stale,
}

internal static class FabLibrarySnapshotAgePolicy
{
    private static readonly TimeSpan FreshThreshold = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WarmThreshold = TimeSpan.FromMinutes(5);

    internal static FabLibrarySnapshotAgeCategory Classify(FabLibrarySessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Classify(snapshot.SnapshotAtUtc, DateTime.UtcNow);
    }

    internal static FabLibrarySnapshotAgeCategory Classify(DateTime snapshotAtUtc, DateTime utcNow)
    {
        var age = utcNow >= snapshotAtUtc
            ? utcNow - snapshotAtUtc
            : TimeSpan.Zero;

        if (age <= FreshThreshold)
        {
            return FabLibrarySnapshotAgeCategory.Fresh;
        }

        if (age <= WarmThreshold)
        {
            return FabLibrarySnapshotAgeCategory.Warm;
        }

        return FabLibrarySnapshotAgeCategory.Stale;
    }
}