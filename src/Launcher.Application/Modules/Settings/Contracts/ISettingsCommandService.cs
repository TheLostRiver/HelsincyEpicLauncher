// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared;

namespace Launcher.Application.Modules.Settings.Contracts;

/// <summary>
/// 配置写入服务。Settings 页面通过此接口修改和持久化配置。
/// </summary>
public interface ISettingsCommandService
{
    /// <summary>更新下载配置</summary>
    Task<Result> UpdateDownloadConfigAsync(DownloadConfig config, CancellationToken ct);

    /// <summary>更新外观配置</summary>
    Task<Result> UpdateAppearanceConfigAsync(AppearanceConfig config, CancellationToken ct);

    /// <summary>更新路径配置</summary>
    Task<Result> UpdatePathConfigAsync(PathConfig config, CancellationToken ct);

    /// <summary>更新网络配置</summary>
    Task<Result> UpdateNetworkConfigAsync(NetworkConfig config, CancellationToken ct);

    /// <summary>更新 Fab 列表配置</summary>
    Task<Result> UpdateFabLibraryConfigAsync(FabLibraryConfig config, CancellationToken ct);

    /// <summary>重置所有配置到默认值</summary>
    Task<Result> ResetToDefaultsAsync(CancellationToken ct);
}
