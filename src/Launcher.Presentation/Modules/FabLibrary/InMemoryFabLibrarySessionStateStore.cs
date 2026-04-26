// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.FabLibrary.Contracts;

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 列表页会话快照的进程内单槽位存储。
/// </summary>
internal sealed class InMemoryFabLibrarySessionStateStore : IFabLibrarySessionStateStore
{
    private readonly object _syncRoot = new();
    private FabLibrarySessionSnapshot? _snapshot;

    public void Save(FabLibrarySessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalizedSnapshot = NormalizeSnapshot(snapshot);

        lock (_syncRoot)
        {
            _snapshot = normalizedSnapshot;
        }
    }

    public bool TryGet(out FabLibrarySessionSnapshot? snapshot)
    {
        lock (_syncRoot)
        {
            snapshot = _snapshot;
            return snapshot is not null;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _snapshot = null;
        }
    }

    public void Trim()
    {
        lock (_syncRoot)
        {
            if (_snapshot is null)
            {
                return;
            }

            _snapshot = NormalizeSnapshot(_snapshot);
        }
    }

    private static FabLibrarySessionSnapshot NormalizeSnapshot(FabLibrarySessionSnapshot snapshot)
    {
        var retainedAssetCount = Math.Min(snapshot.AssetSummaries.Count, FabLibrarySessionSnapshot.MaxRetainedAssetCount);
        var retainedAssets = new FabAssetSummary[retainedAssetCount];
        for (var index = 0; index < retainedAssetCount; index++)
        {
            retainedAssets[index] = snapshot.AssetSummaries[index];
        }

        var retainedCurrentPage = Math.Max(1, Math.Min(snapshot.CurrentPage, FabLibrarySessionSnapshot.MaxRetainedPages));
        var wasPageCapped = retainedCurrentPage != snapshot.CurrentPage;

        return new FabLibrarySessionSnapshot
        {
            Keyword = snapshot.Keyword,
            Category = snapshot.Category,
            SortOrder = snapshot.SortOrder,
            CurrentPage = retainedCurrentPage,
            TotalPages = snapshot.TotalPages,
            HasNextPage = wasPageCapped || snapshot.HasNextPage,
            TotalCount = snapshot.TotalCount,
            VerticalOffset = wasPageCapped ? 0 : snapshot.VerticalOffset,
            SnapshotAtUtc = snapshot.SnapshotAtUtc,
            AccountScopeKey = snapshot.AccountScopeKey,
            AssetSummaries = retainedAssets,
        };
    }
}