// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.EngineVersions.Contracts;

/// <summary>可用引擎版本摘要</summary>
public sealed class EngineVersionSummary
{
    public required string VersionId { get; init; }
    public required string DisplayName { get; init; }
    public long DownloadSize { get; init; }
    public DateTime ReleaseDate { get; init; }
    public bool IsInstalled { get; init; }
    public string? InstalledPath { get; init; }
}

/// <summary>已安装引擎版本摘要</summary>
public sealed class InstalledEngineSummary
{
    public required string VersionId { get; init; }
    public required string DisplayName { get; init; }
    public required string InstallPath { get; init; }
    public long SizeOnDisk { get; init; }
    public DateTime InstalledAt { get; init; }
}
