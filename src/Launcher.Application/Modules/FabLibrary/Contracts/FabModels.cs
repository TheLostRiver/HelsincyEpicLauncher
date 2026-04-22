// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.FabLibrary.Contracts;

// ===== 枚举 =====

/// <summary>Fab 资产类型</summary>
public enum FabAssetType
{
    Model3D,
    Material,
    Blueprint,
    Audio,
    CodePlugin,
    Environment,
    Animation,
    VFX,
    UI,
    Other,
}

/// <summary>资产所有权状态</summary>
public enum AssetOwnershipState
{
    NotOwned,
    Owned,
    Downloaded,
    Installed,
    UpdateAvailable,
}

/// <summary>搜索排序</summary>
public enum FabSortOrder
{
    Relevance,
    Newest,
    PriceLowToHigh,
    PriceHighToLow,
    Rating,
}

// ===== 查询 =====

/// <summary>Fab 搜索查询参数</summary>
public sealed class FabSearchQuery
{
    public string? Keyword { get; init; }
    public string? Category { get; init; }
    public string? EngineVersion { get; init; }
    public FabSortOrder SortOrder { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

// ===== DTO =====

/// <summary>Fab 资产摘要（卡片列表展示）</summary>
public sealed class FabAssetSummary
{
    public required string AssetId { get; init; }
    public required string Title { get; init; }
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string PreviewListingId { get; init; } = string.Empty;
    public string PreviewProductId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public double Rating { get; init; }
    public bool IsOwned { get; init; }
    public bool IsInstalled { get; init; }
    public IReadOnlyList<string> SupportedEngineVersions { get; init; } = [];
}

/// <summary>Fab 资产详情</summary>
public sealed class FabAssetDetail
{
    public required string AssetId { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public double Rating { get; init; }
    public int RatingCount { get; init; }
    public long DownloadSize { get; init; }
    public string LatestVersion { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<string> Screenshots { get; init; } = [];
    public IReadOnlyList<string> SupportedEngineVersions { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? TechnicalDetails { get; init; }
    public bool IsOwned { get; init; }
    public bool IsInstalled { get; init; }
}

/// <summary>资产分类信息</summary>
public sealed class AssetCategoryInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int AssetCount { get; init; }
}

/// <summary>Fab 资产下载链接信息（API 返回）</summary>
public sealed class FabDownloadInfo
{
    public required string AssetId { get; init; }
    public required string DownloadUrl { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string Version { get; init; } = string.Empty;
}
