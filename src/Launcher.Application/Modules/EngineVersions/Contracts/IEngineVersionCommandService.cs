// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared;

namespace Launcher.Application.Modules.EngineVersions.Contracts;

/// <summary>
/// 引擎版本操作服务。
/// </summary>
public interface IEngineVersionCommandService
{
    /// <summary>下载并安装指定引擎版本</summary>
    Task<Result> DownloadAndInstallAsync(string versionId, string installPath, CancellationToken ct);

    /// <summary>卸载引擎版本</summary>
    Task<Result> UninstallAsync(string versionId, CancellationToken ct);

    /// <summary>启动引擎编辑器</summary>
    Task<Result> LaunchEditorAsync(string versionId, CancellationToken ct);
}
