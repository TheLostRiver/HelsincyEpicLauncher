// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Shared.Configuration;
using Microsoft.Extensions.Configuration;

namespace Launcher.Infrastructure.Configuration;

/// <summary>
/// 配置提供器实现。从 IConfiguration 读取强类型配置。
/// </summary>
internal sealed class AppConfigProvider : IAppConfigProvider, IDownloadOptionsProvider
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
        EnsureDirectory(GetConfiguredPathOrDefault("Paths:Data", Path.Combine(_localAppData, "Data")));

    public string LogPath =>
        EnsureDirectory(GetConfiguredPathOrDefault("Paths:Logs", Path.Combine(_localAppData, "Logs")));

    public string CachePath =>
        EnsureDirectory(GetConfiguredPathOrDefault("Paths:Cache", Path.Combine(_localAppData, "Cache")));

    public string DownloadPath =>
        EnsureDirectory(GetConfiguredPathOrDefault("Paths:Downloads", Path.Combine(_localAppData, "Downloads")));

    public string InstallPath =>
        EnsureDirectory(GetConfiguredPathOrDefault("Paths:Installs", Path.Combine(_localAppData, "Installs")));

    public DownloadOptions DownloadOptions => new()
    {
        MaxConcurrentTasks = GetPositiveInt(
            "Downloads:MaxConcurrentTasks",
            GetPositiveInt("Downloads:MaxConcurrent", DownloadOptions.DefaultMaxConcurrentTasks)),
        MaxConcurrentChunksPerTask = GetPositiveInt(
            "Downloads:MaxConcurrentChunksPerTask",
            GetPositiveInt("Downloads:MaxChunksPerTask", DownloadOptions.DefaultMaxConcurrentChunksPerTask)),
        ChunkSizeBytes = GetPositiveLong(
            "Downloads:ChunkSizeBytes",
            GetPositiveInt("Downloads:ChunkSizeMb", 10) * 1024L * 1024L),
        MaxRetryAttempts = GetNonNegativeInt(
            "Downloads:MaxRetryAttempts",
            GetNonNegativeInt("Downloads:MaxRetryCount", DownloadOptions.DefaultMaxRetryAttempts)),
        CheckpointInterval = TimeSpan.FromSeconds(GetPositiveInt(
            "Downloads:CheckpointIntervalSeconds",
            (int)DownloadOptions.DefaultCheckpointInterval.TotalSeconds)),
    };

    public int MaxConcurrentDownloads => DownloadOptions.MaxConcurrentTasks;

    public int MaxChunksPerDownload => DownloadOptions.MaxConcurrentChunksPerTask;

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

    private string GetConfiguredPathOrDefault(string key, string fallbackPath)
    {
        var configuredPath = _configuration[key];
        return string.IsNullOrWhiteSpace(configuredPath)
            ? fallbackPath
            : configuredPath;
    }

    private int GetPositiveInt(string key, int fallback) =>
        int.TryParse(_configuration[key], out int value) && value > 0
            ? value
            : fallback;

    private int GetNonNegativeInt(string key, int fallback) =>
        int.TryParse(_configuration[key], out int value) && value >= 0
            ? value
            : fallback;

    private long GetPositiveLong(string key, long fallback) =>
        long.TryParse(_configuration[key], out long value) && value > 0
            ? value
            : fallback;
}
