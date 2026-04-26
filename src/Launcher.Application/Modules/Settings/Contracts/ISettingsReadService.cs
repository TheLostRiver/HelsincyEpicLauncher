// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Settings.Contracts;

/// <summary>
/// 配置只读查询服务。各模块通过此接口获取当前配置。
/// </summary>
public interface ISettingsReadService
{
    /// <summary>获取下载配置</summary>
    DownloadConfig GetDownloadConfig();

    /// <summary>获取外观配置</summary>
    AppearanceConfig GetAppearanceConfig();

    /// <summary>获取路径配置</summary>
    PathConfig GetPathConfig();

    /// <summary>获取网络配置</summary>
    NetworkConfig GetNetworkConfig();

    /// <summary>获取 Fab 列表配置</summary>
    FabLibraryConfig GetFabLibraryConfig();
}
