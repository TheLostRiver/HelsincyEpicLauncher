// Copyright (c) Helsincy. All rights reserved.

using System.Text.Json;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// 使用受控 WebView2 上下文读取 Fab listing 页面 HTML。
/// </summary>
internal sealed class FabListingPageReadService : IFabListingPageReadService
    , IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<FabListingPageReadService>();

    private const string ListingUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";
    private const int HiddenViewportWidth = 1366;
    private const int HiddenViewportHeight = 900;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);

    private readonly SemaphoreSlim _readLock = new(1, 1);
    private DispatcherQueue? _dispatcherQueue;

    public FabListingPageReadService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public async Task<string?> TryReadListingHtmlAsync(string listingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(listingId))
        {
            return null;
        }

        await _readLock.WaitAsync(ct);
        try
        {
            Logger.Debug("Fab listing 页面读取开始 | ListingId={ListingId}", listingId);
            _dispatcherQueue ??= DispatcherQueue.GetForCurrentThread();
            if (_dispatcherQueue is null)
            {
                Logger.Warning("Fab listing 页面读取失败：当前线程缺少 DispatcherQueue | ListingId={ListingId}", listingId);
                return null;
            }

            var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = ct.Register(() => completionSource.TrySetCanceled(ct));

            if (!_dispatcherQueue.TryEnqueue(() => _ = ReadOnUiThreadAsync(listingId.Trim(), completionSource, ct)))
            {
                Logger.Warning("Fab listing 页面读取失败：无法切回 UI Dispatcher | ListingId={ListingId}", listingId);
                return null;
            }

            var html = await completionSource.Task;
            Logger.Debug("Fab listing 页面读取完成 | ListingId={ListingId} | HasHtml={HasHtml}", listingId, !string.IsNullOrWhiteSpace(html));
            return html;
        }
        finally
        {
            _readLock.Release();
        }
    }

    private static async Task ReadOnUiThreadAsync(string listingId, TaskCompletionSource<string?> completionSource, CancellationToken ct)
    {
        var tempFolder = Path.Combine(
            Path.GetTempPath(),
            "HelsincyEpicLauncher",
            "FabPreviewWebView2",
            Guid.NewGuid().ToString("N"));

        Window? window = null;

        try
        {
            Directory.CreateDirectory(tempFolder);

            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            var host = new Grid();
            host.Children.Add(webView);

            window = new Window
            {
                Title = "FabPreviewProbe",
                Content = host,
            };

            ConfigureHiddenWindow(window);
            window.Activate();

            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: null,
                userDataFolder: tempFolder,
                options: null);
            await webView.EnsureCoreWebView2Async(environment);

            var coreWebView = webView.CoreWebView2;
            if (coreWebView is null)
            {
                completionSource.TrySetResult(null);
                return;
            }

            coreWebView.Settings.IsStatusBarEnabled = false;
            coreWebView.Settings.IsZoomControlEnabled = false;
            coreWebView.Settings.AreDefaultContextMenusEnabled = false;
            coreWebView.Settings.AreDevToolsEnabled = false;

            try
            {
                await coreWebView.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
                await coreWebView.CallDevToolsProtocolMethodAsync(
                    "Network.setUserAgentOverride",
                    JsonSerializer.Serialize(new { userAgent = ListingUserAgent }));
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Fab listing 页面读取设置 User-Agent 失败，将继续使用默认 UA");
            }

            coreWebView.Navigate($"https://www.fab.com/listings/{Uri.EscapeDataString(listingId)}");

            var html = await WaitForListingHtmlAsync(coreWebView, ct);
            completionSource.TrySetResult(html);
        }
        catch (OperationCanceledException)
        {
            completionSource.TrySetCanceled(ct);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "读取 Fab listing 页面失败 | ListingId={ListingId}", listingId);
            completionSource.TrySetResult(null);
        }
        finally
        {
            try
            {
                window?.Close();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "关闭 Fab listing WebView2 窗口失败");
            }

            CleanupTempFolder(tempFolder);
        }
    }

    private static void ConfigureHiddenWindow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Keep the probe off-screen, but give WebView2 a realistic desktop viewport so
        // Fab does not collapse into tiny responsive or lazy-loading branches.
        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, HiddenViewportWidth, HiddenViewportHeight));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }
    }

    private static async Task<string?> WaitForListingHtmlAsync(CoreWebView2 coreWebView, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + ReadTimeout;
        string? lastStableHtml = null;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(PollInterval, ct);

            var html = await ExecuteStringScriptAsync(coreWebView, "document.documentElement?.outerHTML ?? ''");
            if (string.IsNullOrWhiteSpace(html))
            {
                continue;
            }

            var isChallenge = await ExecuteBoolScriptAsync(
                coreWebView,
                "document.querySelector('.cf_challenge_container') !== null || (document.title ?? '').toLowerCase().includes('just a moment')");

            if (!isChallenge)
            {
                lastStableHtml = html;
                if (ContainsPreviewSignal(html))
                {
                    return html;
                }
            }
        }

        return lastStableHtml;
    }

    private static bool ContainsPreviewSignal(string html)
    {
        return html.Contains("https://media.fab.com/image_previews/", StringComparison.OrdinalIgnoreCase)
            || html.Contains("https:\\/\\/media.fab.com\\/image_previews\\/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ExecuteStringScriptAsync(CoreWebView2 coreWebView, string script)
    {
        var raw = await coreWebView.ExecuteScriptAsync(script);
        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string>(raw);
    }

    private static async Task<bool> ExecuteBoolScriptAsync(CoreWebView2 coreWebView, string script)
    {
        var raw = await coreWebView.ExecuteScriptAsync(script);
        return bool.TryParse(raw, out var value) && value;
    }

    private static void CleanupTempFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "清理 Fab listing WebView2 临时目录失败 | Path={Path}", path);
        }
    }

    public void Dispose()
    {
        _readLock.Dispose();
        GC.SuppressFinalize(this);
    }
}