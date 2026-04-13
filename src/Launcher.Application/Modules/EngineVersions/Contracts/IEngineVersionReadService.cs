// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared;

namespace Launcher.Application.Modules.EngineVersions.Contracts;

/// <summary>
/// 引擎版本查询服务。
/// </summary>
public interface IEngineVersionReadService
{
    /// <summary>获取可用引擎版本列表（远程 + 本地合并）</summary>
    Task<Result<IReadOnlyList<EngineVersionSummary>>> GetAvailableVersionsAsync(CancellationToken ct);

    /// <summary>获取本地已安装引擎版本</summary>
    Task<Result<IReadOnlyList<InstalledEngineSummary>>> GetInstalledVersionsAsync(CancellationToken ct);
}
