// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared;

namespace Launcher.Application.Modules.Installations.Contracts;

/// <summary>
/// 安装命令服务。
/// </summary>
public interface IInstallCommandService
{
    /// <summary>安装已下载的资产</summary>
    Task<Result> InstallAsync(InstallRequest request, CancellationToken ct);

    /// <summary>卸载已安装的资产</summary>
    Task<Result> UninstallAsync(string assetId, CancellationToken ct);

    /// <summary>修复损坏的安装</summary>
    Task<Result> RepairAsync(string assetId, CancellationToken ct);
}
