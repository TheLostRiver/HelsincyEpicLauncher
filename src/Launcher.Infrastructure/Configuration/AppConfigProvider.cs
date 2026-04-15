// Copyright (c) Helsincy. All rights reserved.

using Microsoft.Extensions.Configuration;

namespace Launcher.Infrastructure.Configuration;

/// <summary>
/// 配置提供器实现。从 IConfiguration 读取强类型配置。
/// </summary>
internal sealed class AppConfigProvider : Shared.Configuration.IAppConfigProvider
{
    private readonly IConfiguration _configuration;
    private readonly string _localAppData;

    public AppConfigProvider(IConfiguration configuration)
    {
        _configuration = configuration;
        _localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Launcher.Shared.AppConstants.AppName);
    }

    public string AppVersion =>
        _configuration["App:Version"] ?? "0.0.0";

    public string DataPath =>
        EnsureDirectory(_configuration["Paths:Data"]
            ?? Path.Combine(_localAppData, "Data"));

    public string LogPath =>
        EnsureDirectory(_configuration["Paths:Logs"]
            ?? Path.Combine(_localAppData, "Logs"));

    public string CachePath =>
        EnsureDirectory(_configuration["Paths:Cache"]
            ?? Path.Combine(_localAppData, "Cache"));

    public string DownloadPath =>
        EnsureDirectory(_configuration["Paths:Downloads"]
            ?? Path.Combine(_localAppData, "Downloads"));

    public string InstallPath =>
        EnsureDirectory(_configuration["Paths:Installs"]
            ?? Path.Combine(_localAppData, "Installs"));

    public int MaxConcurrentDownloads =>
        int.TryParse(_configuration["Downloads:MaxConcurrent"], out int val) ? val : 3;

    public int MaxChunksPerDownload =>
        int.TryParse(_configuration["Downloads:MaxChunksPerTask"], out int val) ? val : 4;

    /// <summary>
    /// 确保目录存在，返回路径
    /// </summary>
    private static string EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }
}
