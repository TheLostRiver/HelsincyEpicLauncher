// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.FabLibrary.Contracts;

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 列表页会话快照。仅保存可恢复 UI 的窄状态，不持有可变 UI 对象。
/// </summary>
internal sealed class FabLibrarySessionSnapshot
{
    public string Keyword { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public FabSortOrder SortOrder { get; init; } = FabSortOrder.Relevance;

    public int CurrentPage { get; init; } = 1;

    public int TotalPages { get; init; }

    public bool HasNextPage { get; init; }

    public int TotalCount { get; init; }

    public double VerticalOffset { get; init; }

    public DateTime SnapshotAtUtc { get; init; } = DateTime.UtcNow;

    public string AccountScopeKey { get; init; } = string.Empty;

    public IReadOnlyList<FabAssetSummary> AssetSummaries { get; init; } = [];
}