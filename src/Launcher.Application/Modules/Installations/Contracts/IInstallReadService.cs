// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Installations.Contracts;

/// <summary>
/// 安装状态查询服务。
/// </summary>
public interface IInstallReadService
{
    /// <summary>获取资产安装状态</summary>
    Task<InstallStatusSummary?> GetStatusAsync(string assetId, CancellationToken ct);

    /// <summary>获取所有已安装资产</summary>
    Task<IReadOnlyList<InstallStatusSummary>> GetInstalledAsync(CancellationToken ct);
}
