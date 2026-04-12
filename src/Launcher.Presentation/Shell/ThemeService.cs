// Copyright (c) Helsincy. All rights reserved.

using System.Text.Json;
using Launcher.Shared.Configuration;
using Microsoft.UI.Xaml;
using Serilog;

namespace Launcher.Presentation.Shell;

/// <summary>
/// 主题切换服务。管理 Light / Dark / System 三种主题，持久化到本地文件。
/// </summary>
public sealed class ThemeService
{
    private static readonly ILogger Logger = Log.ForContext<ThemeService>();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;
    private FrameworkElement? _rootElement;

    /// <summary>当前主题</summary>
    public ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;

    public ThemeService(IAppConfigProvider configProvider)
    {
        _settingsPath = Path.Combine(configProvider.DataPath, "theme.json");
    }

    /// <summary>
    /// 设置根元素并加载已保存的主题。由 ShellPage 在加载时调用。
    /// </summary>
    public void Initialize(FrameworkElement rootElement)
    {
        _rootElement = rootElement;
        LoadTheme();
        ApplyTheme();
        Logger.Information("主题服务已初始化 | 当前主题: {Theme}", CurrentTheme);
    }

    /// <summary>
    /// 切换主题并持久化
    /// </summary>
    public void SetTheme(ElementTheme theme)
    {
        CurrentTheme = theme;
        ApplyTheme();
        SaveTheme();
        Logger.Information("主题已切换为 {Theme}", theme);
    }

    private void ApplyTheme()
    {
        if (_rootElement is not null)
        {
            _rootElement.RequestedTheme = CurrentTheme;
        }
    }

    private void LoadTheme()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            string json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
            if (settings is not null)
            {
                CurrentTheme = settings.Theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default,
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "加载主题设置失败，使用默认主题");
        }
    }

    private void SaveTheme()
    {
        try
        {
            string dir = Path.GetDirectoryName(_settingsPath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string themeValue = CurrentTheme switch
            {
                ElementTheme.Light => "Light",
                ElementTheme.Dark => "Dark",
                _ => "System",
            };

            var settings = new ThemeSettings { Theme = themeValue };
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "保存主题设置失败");
        }
    }

    private sealed class ThemeSettings
    {
        public string Theme { get; set; } = "System";
    }
}
