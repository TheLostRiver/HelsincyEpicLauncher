// Copyright (c) Helsincy. All rights reserved.

using System.Text.Json;
using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Shared;
using Launcher.Shared.Configuration;
using Serilog;

namespace Launcher.Infrastructure.Settings;

/// <summary>
/// 配置服务实现。管理 user.settings.json 的读写和内存配置状态。
/// 加载优先级：默认值 → appsettings.json → user.settings.json
/// </summary>
internal sealed class SettingsService : ISettingsCommandService, ISettingsReadService
{
    private static readonly ILogger Logger = Log.ForContext<SettingsService>();
    private static readonly JsonSerializerOptions JsonOptions = Launcher.Shared.JsonDefaults.CamelCaseIndented;

    private readonly string _settingsPath;
    private readonly object _lock = new();
    private UserSettings _settings;

    /// <summary>
    /// 配置变更事件。订阅者在配置修改后收到通知。
    /// </summary>
    public event Action<ConfigChangedEvent>? ConfigChanged;

    public SettingsService(IAppConfigProvider configProvider)
    {
        _settingsPath = Path.Combine(configProvider.DataPath, "user.settings.json");
        _settings = LoadSettings();
        Logger.Information("配置服务已初始化 | 配置文件: {Path}", _settingsPath);
    }

    // === ISettingsReadService ===

    public DownloadConfig GetDownloadConfig()
    {
        lock (_lock)
        {
            return Clone(_settings.Download);
        }
    }

    public AppearanceConfig GetAppearanceConfig()
    {
        lock (_lock)
        {
            return Clone(_settings.Appearance);
        }
    }

    public PathConfig GetPathConfig()
    {
        lock (_lock)
        {
            return Clone(_settings.Paths);
        }
    }

    public NetworkConfig GetNetworkConfig()
    {
        lock (_lock)
        {
            return Clone(_settings.Network);
        }
    }

    public FabLibraryConfig GetFabLibraryConfig()
    {
        lock (_lock)
        {
            return Clone(_settings.FabLibrary);
        }
    }

    // === ISettingsCommandService ===

    public Task<Result> UpdateDownloadConfigAsync(DownloadConfig config, CancellationToken ct)
    {
        return UpdateSectionAsync("Download", s => s.Download = Clone(config));
    }

    public Task<Result> UpdateAppearanceConfigAsync(AppearanceConfig config, CancellationToken ct)
    {
        return UpdateSectionAsync("Appearance", s => s.Appearance = Clone(config));
    }

    public Task<Result> UpdatePathConfigAsync(PathConfig config, CancellationToken ct)
    {
        return UpdateSectionAsync("Paths", s => s.Paths = Clone(config));
    }

    public Task<Result> UpdateNetworkConfigAsync(NetworkConfig config, CancellationToken ct)
    {
        return UpdateSectionAsync("Network", s => s.Network = Clone(config));
    }

    public Task<Result> UpdateFabLibraryConfigAsync(FabLibraryConfig config, CancellationToken ct)
    {
        return UpdateSectionAsync("FabLibrary", s => s.FabLibrary = Clone(config));
    }

    public Task<Result> ResetToDefaultsAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            _settings = new UserSettings();
        }

        try
        {
            SaveSettings();
            Logger.Information("配置已重置为默认值");

            ConfigChanged?.Invoke(new ConfigChangedEvent("Download", _settings.Download));
            ConfigChanged?.Invoke(new ConfigChangedEvent("Appearance", _settings.Appearance));
            ConfigChanged?.Invoke(new ConfigChangedEvent("Paths", _settings.Paths));
            ConfigChanged?.Invoke(new ConfigChangedEvent("Network", _settings.Network));
            ConfigChanged?.Invoke(new ConfigChangedEvent("FabLibrary", _settings.FabLibrary));

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "重置配置失败");
            return Task.FromResult(Result.Fail(new Error
            {
                Code = "Settings.ResetFailed",
                UserMessage = "重置配置失败",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            }));
        }
    }

    // === 私有方法 ===

    private Task<Result> UpdateSectionAsync(string section, Action<UserSettings> updater)
    {
        lock (_lock)
        {
            updater(_settings);
        }

        try
        {
            SaveSettings();
            Logger.Information("配置已更新 | Section={Section}", section);

            object newConfig = section switch
            {
                "Download" => _settings.Download,
                "Appearance" => _settings.Appearance,
                "Paths" => _settings.Paths,
                "Network" => _settings.Network,
                "FabLibrary" => _settings.FabLibrary,
                _ => _settings,
            };
            ConfigChanged?.Invoke(new ConfigChangedEvent(section, newConfig));

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "保存配置失败 | Section={Section}", section);
            return Task.FromResult(Result.Fail(new Error
            {
                Code = "Settings.SaveFailed",
                UserMessage = "保存配置失败",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            }));
        }
    }

    /// <summary>
    /// 从 user.settings.json 加载配置；文件不存在则返回默认值。
    /// </summary>
    private UserSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                Logger.Debug("用户配置文件不存在，使用默认值 | {Path}", _settingsPath);
                return new UserSettings();
            }

            string json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
            Logger.Debug("用户配置已加载 | {Path}", _settingsPath);
            return settings ?? new UserSettings();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "加载用户配置失败，使用默认值 | {Path}", _settingsPath);
            return new UserSettings();
        }
    }

    /// <summary>
    /// 将当前配置持久化到 user.settings.json。
    /// </summary>
    private void SaveSettings()
    {
        UserSettings snapshot;
        lock (_lock)
        {
            snapshot = Clone(_settings);
        }

        string dir = Path.GetDirectoryName(_settingsPath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    /// <summary>
    /// 深拷贝配置对象，避免外部修改影响内部状态
    /// </summary>
    private static T Clone<T>(T source) where T : class
    {
        string json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }
}
