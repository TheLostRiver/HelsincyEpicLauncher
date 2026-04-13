// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Installations;

namespace Launcher.Application.Modules.Installations.Contracts;

/// <summary>
/// 安装请求
/// </summary>
public sealed class InstallRequest
{
    public required string AssetId { get; init; }
    public required string AssetName { get; init; }
    public required string SourcePath { get; init; }
    public required string InstallPath { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string AssetType { get; init; } = "FabAsset";
}

/// <summary>
/// 安装状态摘要 DTO
/// </summary>
public sealed class InstallStatusSummary
{
    public required string AssetId { get; init; }
    public required string AssetName { get; init; }
    public required string InstallPath { get; init; }
    public required string Version { get; init; }
    public long SizeOnDisk { get; init; }
    public DateTime InstalledAt { get; init; }
    public InstallState State { get; init; }
    public bool NeedsRepair { get; init; }
}

/// <summary>
/// 校验报告
/// </summary>
public sealed class VerificationReport
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> MissingFiles { get; init; } = [];
    public IReadOnlyList<string> CorruptedFiles { get; init; } = [];
    public long TotalFilesChecked { get; init; }
}

/// <summary>
/// 校验进度
/// </summary>
public sealed class VerificationProgress
{
    public long CheckedFiles { get; init; }
    public long TotalFiles { get; init; }
    public required string CurrentFile { get; init; }
}

// ===== 事件 =====

public sealed record InstallationCompletedEvent(string AssetId, string InstallPath);
public sealed record InstallationFailedEvent(string AssetId, string ErrorMessage);
public sealed record UninstallCompletedEvent(string AssetId);
public sealed record RepairCompletedEvent(string AssetId, int RepairedFileCount);
