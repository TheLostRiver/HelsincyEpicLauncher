// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Common;
using Launcher.Shared;

namespace Launcher.Domain.Installations;

/// <summary>
/// 安装任务领域实体。管理安装状态机和安装相关数据。
/// </summary>
public sealed class Installation : Entity<string>
{
    private readonly InstallStateMachine _stateMachine;

    public string AssetId { get; }
    public string AssetName { get; }
    public string Version { get; }
    public string InstallPath { get; }
    public long SizeBytes { get; private set; }
    public string AssetType { get; }
    public DateTimeOffset InstalledAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public InstallState State => _stateMachine.Current;
    public string? LastError { get; private set; }

    /// <summary>
    /// 创建新安装记录
    /// </summary>
    public Installation(
        string assetId,
        string assetName,
        string version,
        string installPath,
        string assetType = "FabAsset")
    {
        Id = Guid.NewGuid().ToString();
        AssetId = assetId;
        AssetName = assetName;
        Version = version;
        InstallPath = installPath;
        AssetType = assetType;
        InstalledAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
        _stateMachine = new InstallStateMachine(InstallState.NotInstalled);
    }

    /// <summary>
    /// 从持久化恢复
    /// </summary>
    public Installation(
        string id,
        string assetId,
        string assetName,
        string version,
        string installPath,
        long sizeBytes,
        string assetType,
        InstallState state,
        DateTimeOffset installedAt,
        DateTimeOffset updatedAt,
        string? lastError = null)
    {
        Id = id;
        AssetId = assetId;
        AssetName = assetName;
        Version = version;
        InstallPath = installPath;
        SizeBytes = sizeBytes;
        AssetType = assetType;
        InstalledAt = installedAt;
        UpdatedAt = updatedAt;
        LastError = lastError;
        _stateMachine = new InstallStateMachine(state);
    }

    public Result TransitionTo(InstallState target)
    {
        var result = _stateMachine.TransitionTo(target);
        if (result.IsSuccess)
            UpdatedAt = DateTimeOffset.UtcNow;
        return result;
    }

    public bool CanTransitionTo(InstallState target) => _stateMachine.CanTransitionTo(target);

    public void SetSize(long sizeBytes)
    {
        SizeBytes = sizeBytes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetError(string error)
    {
        LastError = error;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ClearError()
    {
        LastError = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
