// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Settings.Contracts;

/// <summary>
/// 下载配置
/// </summary>
public sealed class DownloadConfig
{
    /// <summary>最大并行下载任务数</summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>每个任务的最大并行分块数</summary>
    public int MaxChunksPerDownload { get; set; } = 4;

    /// <summary>下载完成后自动安装</summary>
    public bool AutoInstall { get; set; }
}

/// <summary>
/// 外观配置
/// </summary>
public sealed class AppearanceConfig
{
    /// <summary>主题：System / Light / Dark</summary>
    public string Theme { get; set; } = "System";

    /// <summary>语言</summary>
    public string Language { get; set; } = "zh-CN";
}

/// <summary>
/// 路径配置
/// </summary>
public sealed class PathConfig
{
    /// <summary>默认下载路径</summary>
    public string DownloadPath { get; set; } = string.Empty;

    /// <summary>默认安装路径</summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>缓存目录</summary>
    public string CachePath { get; set; } = string.Empty;
}

/// <summary>
/// 网络配置
/// </summary>
public sealed class NetworkConfig
{
    /// <summary>HTTP 代理地址，空字符串表示不使用代理</summary>
    public string ProxyAddress { get; set; } = string.Empty;

    /// <summary>HTTP 超时（秒）</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>启用 CDN 回退</summary>
    public bool EnableCdnFallback { get; set; } = true;
}

/// <summary>
/// Fab 列表预热配置
/// </summary>
public sealed class FabLibraryConfig
{
    /// <summary>启动后自动预热 Fab 列表</summary>
    public bool AutoWarmOnStartup { get; set; }
}
