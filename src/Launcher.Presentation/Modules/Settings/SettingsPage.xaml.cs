// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace Launcher.Presentation.Modules.Settings;

/// <summary>
/// 设置页面。通过 DI 解析 SettingsViewModel 并连接主题实时切换。
/// </summary>
public sealed partial class SettingsPage : Page
{
    private static readonly ILogger Logger = Log.ForContext<SettingsPage>();
    private readonly IDialogService _dialogService;
    private readonly IFolderPickerService _folderPickerService;
    private bool _isFabWarmupToggleReady;

    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = ViewModelLocator.Resolve<SettingsViewModel>();
        _dialogService = ViewModelLocator.Resolve<IDialogService>();
        _folderPickerService = ViewModelLocator.Resolve<IFolderPickerService>();
        this.InitializeComponent();

        // 监听主题变更请求，实时切换
        ViewModel.ThemeChangeRequested += OnThemeChangeRequested;
        Logger.Debug("SettingsPage 已创建");
    }

    private void PageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // ScrollViewer 在横向方向会给子元素较宽松的测量约束。
        // 这里把宿主宽度钉到当前视口，避免长路径文本把整页表单整体撑出可视区域。
        double viewportWidth = Math.Max(0, e.NewSize.Width - PageScrollViewer.Padding.Left - PageScrollViewer.Padding.Right);
        ViewportHost.Width = viewportWidth;
    }

    private void OnThemeChangeRequested(string theme)
    {
        var themeService = ViewModelLocator.Resolve<ThemeService>();
        var elementTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        themeService.SetTheme(elementTheme);
    }

    private async void DownloadPathBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await SelectPathAsync(path => ViewModel.DownloadPath = path, "默认下载路径");
    }

    private async void InstallPathBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await SelectPathAsync(path => ViewModel.InstallPath = path, "默认安装路径");
    }

    private async void CachePathBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await SelectPathAsync(path => ViewModel.CachePath = path, "缓存路径");
    }

    private void FabWarmupToggleSwitch_Loaded(object sender, RoutedEventArgs e)
    {
        _isFabWarmupToggleReady = true;
    }

    private async void FabWarmupToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isFabWarmupToggleReady)
        {
            return;
        }

        await ViewModel.SaveFabLibraryConfigCommand.ExecuteAsync(null);
    }

    private async Task SelectPathAsync(Action<string> applyPath, string label)
    {
        try
        {
            string? selectedPath = await _folderPickerService.PickFolderAsync(CancellationToken.None);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                Logger.Debug("用户取消路径选择 | Label={Label}", label);
                return;
            }

            applyPath(selectedPath);
            Logger.Information("路径已通过选择器更新 | Label={Label} Path={Path}", label, selectedPath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "打开路径选择器失败 | Label={Label}", label);
            await _dialogService.ShowErrorAsync("打开路径选择器失败", $"{label}选择失败，请稍后重试或手动输入路径。");
        }
    }
}
