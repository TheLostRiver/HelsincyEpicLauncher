// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Domain.Installations;

/// <summary>
/// 安装清单。记录一个资产的全部文件信息，用于校验和修复。
/// </summary>
public sealed class InstallManifest
{
    public required string AssetId { get; init; }
    public required string Version { get; init; }
    public IReadOnlyList<ManifestFileEntry> Files { get; init; } = [];
    public long TotalSize { get; init; }
}

/// <summary>
/// 清单中单个文件条目
/// </summary>
public sealed class ManifestFileEntry
{
    public required string RelativePath { get; init; }
    public long Size { get; init; }
    public required string Hash { get; init; }  // SHA-256
}
