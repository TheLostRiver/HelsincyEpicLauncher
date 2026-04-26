// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.FabLibrary.Contracts;
using Serilog;

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 列表页会话快照的进程内单槽位存储。
/// </summary>
internal sealed class InMemoryFabLibrarySessionStateStore : IFabLibrarySessionStateStore
{
    private static readonly ILogger Logger = Log.ForContext<InMemoryFabLibrarySessionStateStore>();

    private readonly object _syncRoot = new();
    private FabLibrarySessionSnapshot? _snapshot;

    public void Save(FabLibrarySessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalizedSnapshot = NormalizeSnapshot(snapshot);
        var wasTrimmed = WasNormalized(snapshot, normalizedSnapshot);

        lock (_syncRoot)
        {
            _snapshot = normalizedSnapshot;
        }

        Logger.Information(
            "Fab 会话快照已保存 | Count={Count} CurrentPage={CurrentPage} WasTrimmed={WasTrimmed}",
            normalizedSnapshot.AssetSummaries.Count,
            normalizedSnapshot.CurrentPage,
            wasTrimmed);
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
        FabLibrarySessionSnapshot? previousSnapshot;
        lock (_syncRoot)
        {
            previousSnapshot = _snapshot;
            _snapshot = null;
        }

        Logger.Information(
            "Fab 会话快照已清理 | HadSnapshot={HadSnapshot} PreviousCount={PreviousCount} PreviousPage={PreviousPage}",
            previousSnapshot is not null,
            previousSnapshot?.AssetSummaries.Count ?? 0,
            previousSnapshot?.CurrentPage ?? 0);
    }

    public void Trim()
    {
        FabLibrarySessionSnapshot? originalSnapshot;
        FabLibrarySessionSnapshot? normalizedSnapshot;
        lock (_syncRoot)
        {
            if (_snapshot is null)
            {
                Logger.Information("Fab 会话快照执行 Trim 时没有可裁剪内容");
                return;
            }

            originalSnapshot = _snapshot;
            normalizedSnapshot = NormalizeSnapshot(_snapshot);
            _snapshot = normalizedSnapshot;
        }

        Logger.Information(
            "Fab 会话快照已执行 Trim | OriginalCount={OriginalCount} RetainedCount={RetainedCount} OriginalPage={OriginalPage} RetainedPage={RetainedPage} WasTrimmed={WasTrimmed}",
            originalSnapshot!.AssetSummaries.Count,
            normalizedSnapshot!.AssetSummaries.Count,
            originalSnapshot.CurrentPage,
            normalizedSnapshot.CurrentPage,
            WasNormalized(originalSnapshot, normalizedSnapshot));
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

    private static bool WasNormalized(FabLibrarySessionSnapshot originalSnapshot, FabLibrarySessionSnapshot normalizedSnapshot)
    {
        return originalSnapshot.AssetSummaries.Count != normalizedSnapshot.AssetSummaries.Count
            || originalSnapshot.CurrentPage != normalizedSnapshot.CurrentPage
            || originalSnapshot.VerticalOffset != normalizedSnapshot.VerticalOffset
            || originalSnapshot.HasNextPage != normalizedSnapshot.HasNextPage;
    }
}