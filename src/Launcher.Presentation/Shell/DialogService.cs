// Copyright (c) Helsincy. All rights reserved.

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace Launcher.Presentation.Shell;

/// <summary>
/// 对话框服务实现。使用 WinUI 3 ContentDialog 显示确认、信息和错误对话框。
/// 需在 UI 线程上调用。
/// </summary>
public sealed class DialogService : IDialogService
{
    private static readonly ILogger Logger = Log.ForContext<DialogService>();
    private const int EmbeddedLoginWindowHorizontalMargin = 96;
    private const int EmbeddedLoginWindowVerticalMargin = 96;
    private const int EmbeddedLoginWindowPreferredWidth = 1440;
    private const int EmbeddedLoginWindowPreferredHeight = 960;
    private const int EmbeddedLoginWindowMinimumWidth = 960;
    private const int EmbeddedLoginWindowMinimumHeight = 720;
    private XamlRoot? _xamlRoot;

    private static int ResolveEmbeddedLoginWindowExtent(int availableSize, int preferredMinimumSize, int preferredSize, int fallbackSize)
    {
        if (availableSize <= 0)
        {
            return fallbackSize;
        }

        if (availableSize >= preferredMinimumSize)
        {
            return Math.Min(preferredSize, availableSize);
        }

        return availableSize;
    }

    private static void ConfigureEmbeddedLoginWindow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            windowId,
            Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var windowWidth = ResolveEmbeddedLoginWindowExtent(
            workArea.Width - EmbeddedLoginWindowHorizontalMargin,
            EmbeddedLoginWindowMinimumWidth,
            EmbeddedLoginWindowPreferredWidth,
            1280);
        var windowHeight = ResolveEmbeddedLoginWindowExtent(
            workArea.Height - EmbeddedLoginWindowVerticalMargin,
            EmbeddedLoginWindowMinimumHeight,
            EmbeddedLoginWindowPreferredHeight,
            840);

        var x = workArea.X + Math.Max(0, (workArea.Width - windowWidth) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - windowHeight) / 2);

        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, windowWidth, windowHeight));

        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
        }
    }

    /// <summary>
    /// 设置 XamlRoot。由 ShellPage 在加载时调用，ContentDialog 显示需要此引用。
    /// </summary>
    public void SetXamlRoot(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
        Logger.Debug("DialogService XamlRoot 已设置");
    }

    public async Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "确认", string cancelText = "取消")
    {
        Logger.Information("显示确认对话框 | {Title}", title);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
        };

        var result = await dialog.ShowAsync();
        bool confirmed = result == ContentDialogResult.Primary;

        Logger.Information("确认对话框结果 | {Title} → {Confirmed}", title, confirmed);
        return confirmed;
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        Logger.Information("显示信息对话框 | {Title}", title);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _xamlRoot,
        };

        await dialog.ShowAsync();
    }

    public async Task ShowErrorAsync(string title, string message, bool canRetry = false)
    {
        Logger.Warning("显示错误对话框 | {Title} CanRetry={CanRetry}", title, canRetry);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = canRetry ? "取消" : "确定",
            DefaultButton = canRetry ? ContentDialogButton.Primary : ContentDialogButton.Close,
            XamlRoot = _xamlRoot,
        };

        if (canRetry)
        {
            dialog.PrimaryButtonText = "重试";
        }

        await dialog.ShowAsync();
    }

    public async Task<string?> ShowTextInputAsync(string title, string message, string placeholder = "", string confirmText = "确认", string cancelText = "取消")
    {
        Logger.Information("显示文本输入对话框 | {Title}", title);

        var inputBox = new TextBox
        {
            PlaceholderText = placeholder,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            MinWidth = 360,
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                },
                inputBox,
            },
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? inputBox.Text?.Trim() : null;
    }

    public async Task<Result<string>> ShowEpicExchangeCodeLoginAsync(AuthExchangeCodeLoginContext loginContext, CancellationToken ct = default)
    {
        Logger.Information("显示 Epic 嵌入式登录窗口");

        if (_xamlRoot is null)
        {
            return Result.Fail<string>(new Error
            {
                Code = "AUTH_WEBVIEW_XAML_ROOT_MISSING",
                UserMessage = "登录窗口尚未准备完成，请稍后重试",
                TechnicalMessage = "DialogService XamlRoot is not set before showing Epic embedded login window.",
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }

        var statusText = new TextBlock
        {
            Text = "正在加载安全登录窗口...",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        var progressRing = new ProgressRing
        {
            IsActive = true,
            Width = 20,
            Height = 20,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var embeddedLoginUserDataFolder = Path.Combine(
            Path.GetTempPath(),
            "HelsincyEpicLauncher",
            "AuthWebView2",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(embeddedLoginUserDataFolder);

        var webView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinWidth = 720,
            MinHeight = 520,
        };

        var cancelButton = new Button
        {
            Content = "取消",
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var footer = new Grid
        {
            Margin = new Thickness(0, 12, 0, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children =
            {
                new TextBlock
                {
                    Text = "若人机验证区域仍紧凑，可直接最大化此窗口后继续。",
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"],
                },
                cancelButton,
            },
        };

        Grid.SetColumn(cancelButton, 1);

        var content = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
            },
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                progressRing,
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "请在此窗口中完成 Epic 登录",
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        },
                        statusText,
                    },
                },
            },
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(webView, 1);
        Grid.SetRow(footer, 2);
        content.Children.Add(header);
        content.Children.Add(webView);
        content.Children.Add(footer);

        var loginWindow = new Window
        {
            Title = "登录 Epic Games",
            Content = content,
        };

        ConfigureEmbeddedLoginWindow(loginWindow);

        string? exchangeCode = null;
        Error? loginError = null;
        bool windowClosed = false;
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Uri? currentDocumentUri = Uri.TryCreate(loginContext.LoginUrl, UriKind.Absolute, out var initialUri)
            ? initialUri
            : null;

        void CompleteWindowLifetime()
        {
            completionSource.TrySetResult();
        }

        void CloseLoginWindow()
        {
            if (windowClosed)
            {
                return;
            }

            windowClosed = true;
            try
            {
                loginWindow.Close();
            }
            catch (InvalidOperationException)
            {
            }

            CompleteWindowLifetime();
        }

        void SetLoginError(Error error)
        {
            loginError ??= error;
            CloseLoginWindow();
        }

        cancelButton.Click += (_, _) => CloseLoginWindow();
        loginWindow.Closed += (_, _) =>
        {
            windowClosed = true;
            CompleteWindowLifetime();
        };

        using var cancellationRegistration = ct.Register(() =>
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                SetLoginError(new Error
                {
                    Code = "AUTH_WEBVIEW_LOGIN_CANCELLED",
                    UserMessage = "已取消登录",
                    TechnicalMessage = "Embedded login window was cancelled by cancellation token.",
                    CanRetry = true,
                    Severity = ErrorSeverity.Warning,
                });
            });
        });

        async Task InitializeWebViewAsync()
        {
            try
            {
                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    browserExecutableFolder: null,
                    userDataFolder: embeddedLoginUserDataFolder,
                    options: null);
                await webView.EnsureCoreWebView2Async(environment);

                var coreWebView = webView.CoreWebView2;
                if (coreWebView is null)
                {
                    SetLoginError(new Error
                    {
                        Code = "AUTH_WEBVIEW_INIT_FAILED",
                        UserMessage = "嵌入式登录初始化失败，请重试",
                        TechnicalMessage = "WebView2 CoreWebView2 instance is null after EnsureCoreWebView2Async.",
                        CanRetry = true,
                        Severity = ErrorSeverity.Error,
                    });
                    return;
                }

                coreWebView.Settings.IsStatusBarEnabled = false;
                coreWebView.Settings.IsZoomControlEnabled = false;
                coreWebView.Settings.AreDefaultContextMenusEnabled = false;
                coreWebView.Settings.AreDevToolsEnabled = false;

                if (!string.IsNullOrWhiteSpace(loginContext.UserAgent))
                {
                    try
                    {
                        await coreWebView.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
                        await coreWebView.CallDevToolsProtocolMethodAsync(
                            "Network.setUserAgentOverride",
                            JsonSerializer.Serialize(new { userAgent = loginContext.UserAgent }));
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "设置嵌入式登录 User-Agent 失败，将继续尝试默认 User-Agent");
                    }
                }

                try
                {
                    await coreWebView.Profile.ClearBrowsingDataAsync();
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "清理嵌入式登录会话数据失败，将继续尝试新的 WebView2 配置目录");
                }

                await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(EpicLoginWebViewBridge.BootstrapScript);

                coreWebView.WebMessageReceived += (_, args) =>
                {
                    if (!EpicLoginWebViewBridge.TryParseMessage(args.TryGetWebMessageAsString(), out var message) || message is null)
                    {
                        return;
                    }

                    if (!EpicLoginWebViewBridge.IsTrustedEpicUri(currentDocumentUri))
                    {
                        Logger.Warning("忽略来自非受信任页面的嵌入式登录消息 | Uri={Uri} | Type={Type}", currentDocumentUri, message.Type);
                        return;
                    }

                    if (string.Equals(message.Type, "exchange_code", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(message.ExchangeCode))
                        {
                            Logger.Warning("嵌入式登录桥接收到了空 exchange code");
                            return;
                        }

                        exchangeCode = message.ExchangeCode.Trim();
                        Logger.Information("嵌入式登录已捕获 exchange code，准备完成登录");
                        CloseLoginWindow();
                        return;
                    }

                    if (string.Equals(message.Type, "launch_external_url", StringComparison.OrdinalIgnoreCase)
                        && Uri.TryCreate(message.Url, UriKind.Absolute, out var externalUri))
                    {
                        if (!EpicLoginWebViewBridge.IsTrustedExternalLaunchUri(externalUri))
                        {
                            Logger.Warning("拒绝打开非受信任的嵌入式登录外链 | Url={Url}", externalUri);
                            return;
                        }

                        Logger.Information("嵌入式登录请求打开外部链接 | Url={Url}", externalUri);
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = externalUri.AbsoluteUri,
                                UseShellExecute = true,
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "打开嵌入式登录外部链接失败");
                        }
                    }
                };

                coreWebView.NavigationStarting += (_, args) =>
                {
                    currentDocumentUri = Uri.TryCreate(args.Uri, UriKind.Absolute, out var nextUri)
                        ? nextUri
                        : null;
                    progressRing.IsActive = true;
                    statusText.Text = "正在连接 Epic 登录服务...";
                    Logger.Debug("嵌入式登录开始导航 | Uri={Uri}", args.Uri);
                };

                coreWebView.NavigationCompleted += (_, args) =>
                {
                    currentDocumentUri = Uri.TryCreate(coreWebView.Source, UriKind.Absolute, out var completedUri)
                        ? completedUri
                        : currentDocumentUri;
                    progressRing.IsActive = false;

                    if (!args.IsSuccess)
                    {
                        SetLoginError(new Error
                        {
                            Code = "AUTH_WEBVIEW_NAVIGATION_FAILED",
                            UserMessage = "嵌入式登录页面加载失败，请重试",
                            TechnicalMessage = $"WebView2 navigation failed with status '{args.WebErrorStatus}'.",
                            CanRetry = true,
                            Severity = ErrorSeverity.Warning,
                        });
                        return;
                    }

                    statusText.Text = "请继续在此窗口中完成 Epic 登录。应用不会要求你手动输入敏感信息。";
                };

                coreWebView.Navigate(loginContext.LoginUrl);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "初始化嵌入式登录 WebView2 失败");
                SetLoginError(new Error
                {
                    Code = "AUTH_WEBVIEW_INIT_EXCEPTION",
                    UserMessage = "嵌入式登录初始化失败，请重试",
                    TechnicalMessage = ex.Message,
                    CanRetry = true,
                    Severity = ErrorSeverity.Error,
                });
            }
        }

        loginWindow.Activate();
        _ = InitializeWebViewAsync();

        await completionSource.Task;
        if (!string.IsNullOrWhiteSpace(exchangeCode))
        {
            CleanupEmbeddedLoginUserDataFolder(embeddedLoginUserDataFolder);
            return Result.Ok(exchangeCode);
        }

        if (loginError is not null)
        {
            CleanupEmbeddedLoginUserDataFolder(embeddedLoginUserDataFolder);
            return Result.Fail<string>(loginError);
        }

        CleanupEmbeddedLoginUserDataFolder(embeddedLoginUserDataFolder);

        return Result.Fail<string>(new Error
        {
            Code = "AUTH_WEBVIEW_LOGIN_CANCELLED",
            UserMessage = "已取消登录",
            TechnicalMessage = "User closed embedded Epic login window without completing the flow.",
            CanRetry = true,
            Severity = ErrorSeverity.Warning,
        });
    }

    private static void CleanupEmbeddedLoginUserDataFolder(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "清理嵌入式登录 WebView2 临时目录失败 | Path={Path}", folderPath);
        }
    }

    public Task<TResult?> ShowCustomAsync<TResult>(object dialogViewModel)
    {
        // 自定义对话框将在后续任务中实现
        Logger.Warning("ShowCustomAsync 尚未实现");
        return Task.FromResult<TResult?>(default);
    }
}
