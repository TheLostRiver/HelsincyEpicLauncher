// Copyright (c) Helsincy. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Launcher.Application;
using Launcher.Background;
using Launcher.Domain;
using Launcher.Infrastructure;
using Launcher.Presentation;
using Launcher.Shared.Configuration;
using Launcher.Shared.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Globalization;
using System.IO.Pipes;

namespace Launcher.App;

/// <summary>
/// WinUI 3 应用入口。管理单实例、DI 容器、日志初始化和启动流程。
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    /// <summary>
    /// 单实例互斥体名称
    /// </summary>
    private const string MutexName = "HelsincyEpicLauncher_SingleInstance";

    /// <summary>
    /// 命名管道名称（用于实例间通信）
    /// </summary>
    private const string PipeName = "HelsincyEpicLauncher_Pipe";

    private static Mutex? _mutex;
    private MainWindow? _mainWindow;
    private TrayIconManager? _trayIcon;
    private readonly CancellationTokenSource _appCts = new();

    /// <summary>
    /// 全局服务提供器
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// WinUI 3 应用启动入口
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var appStartSw = System.Diagnostics.Stopwatch.StartNew();

        // === Phase 0：单实例检查 + 最小可显示 ===
        if (!EnsureSingleInstance())
        {
            Log.Information("检测到已有实例运行，已通知前台，退出当前实例");
            Environment.Exit(0);
            return;
        }

        // 启动管道监听（接收第二实例的激活通知）
        StartPipeListener();

        // === Phase 1：核心初始化（DI + 配置 + 日志 + 数据库） ===
        InitializeCoreServices();

        // 创建主窗口
        _mainWindow = new MainWindow();

        // 初始化系统托盘
        InitializeTrayIcon();

        // 拦截窗口关闭 → 最小化到托盘
        ConfigureCloseToTray();

        _mainWindow.Activate();
        _mainWindow.HideLoadingIndicator();

        appStartSw.Stop();
        Log.Information("主窗口已显示 | 冷启动耗时 {ElapsedMs}ms | 系统托盘图标已就绪",
            appStartSw.ElapsedMilliseconds);

        // === Phase 2（后台）：会话恢复 + 下载恢复 ===
        // === Phase 3（延迟）：后台服务启动（不阻塞窗口显示） ===
        _ = Task.Run(() => StartPostLaunchAsync(_appCts.Token));
    }

    /// <summary>
    /// 启动后异步流程：Phase 2 恢复 → Phase 3 后台服务
    /// </summary>
    private static async Task StartPostLaunchAsync(CancellationToken ct)
    {
        // 短暂等待 UI 线程完成首帧渲染
        await Task.Delay(200, ct).ConfigureAwait(false);

        await InitializePhase2Async(ct).ConfigureAwait(false);
        await StartBackgroundServicesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Phase 2：会话恢复 + 下载恢复
    /// </summary>
    private static async Task InitializePhase2Async(CancellationToken ct)
    {
        try
        {
            using var _ = new OperationTimer(Log.Logger, "Phase 2：恢复步骤");

            var authService = Services.GetRequiredService<Launcher.Application.Modules.Auth.Contracts.IAuthService>();
            await authService.TryRestoreSessionAsync(ct).ConfigureAwait(false);
            Log.Information("会话恢复完成");

            var downloadOrchestrator = Services.GetRequiredService<Launcher.Application.Modules.Downloads.Contracts.IDownloadOrchestrator>();
            await downloadOrchestrator.RecoverAsync(ct).ConfigureAwait(false);
            Log.Information("下载任务恢复完成");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Phase 2 恢复失败");
        }
    }

    /// <summary>
    /// Phase 3：在后台线程启动所有后台 Worker
    /// </summary>
    private static Task StartBackgroundServicesAsync(CancellationToken ct)
    {
        try
        {
            using var _ = new OperationTimer(Log.Logger, "后台服务启动");

            var tokenRefresh = Services.GetRequiredService<Launcher.Background.Auth.TokenRefreshBackgroundService>();
            tokenRefresh.Start();

            var autoInstall = Services.GetRequiredService<Launcher.Background.Installations.AutoInstallWorker>();
            autoInstall.Start();

            var updateWorker = Services.GetRequiredService<Launcher.Background.Updates.AppUpdateWorker>();
            updateWorker.Start();

            var networkWorker = Services.GetRequiredService<Launcher.Background.Network.NetworkMonitorWorker>();
            networkWorker.Start();

            Log.Information("所有后台服务已启动");

            // TODO: Phase 3 - 缩略图预热（预加载首屏缩略图）
            // TODO: Phase 3 - 诊断信息收集（磁盘空间、内存等）
            // TODO: Phase 3 - Fab 资产目录刷新（远程同步）
            // TODO: Phase 3 - 清理上次崩溃遗留的临时文件
        }
        catch (Exception ex)
        {
            Log.Error(ex, "后台服务启动失败");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 初始化系统托盘图标
    /// </summary>
    private void InitializeTrayIcon()
    {
        _trayIcon = new TrayIconManager();
        _trayIcon.Initialize();

        _trayIcon.ShowRequested += () =>
        {
            ActivateMainWindow();
        };

        _trayIcon.ExitRequested += () =>
        {
            Log.Information("用户从托盘请求退出应用");
            _appCts.Cancel();
            _trayIcon.Dispose();
            _mainWindow?.Close();
            Environment.Exit(0);
        };
    }

    /// <summary>
    /// 配置关闭按钮行为：最小化到托盘而非关闭应用
    /// </summary>
    private void ConfigureCloseToTray()
    {
        if (_mainWindow is null) return;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        appWindow.Closing += (sender, e) =>
        {
            e.Cancel = true;
            sender.Hide();
            Log.Information("窗口已最小化到系统托盘");
        };
    }

    /// <summary>
    /// 单实例检查。如果已有实例运行，通过命名管道通知并返回 false。
    /// </summary>
    private static bool EnsureSingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out bool isNew);
        if (!isNew)
        {
            // 通知已有实例激活窗口
            NotifyExistingInstance();
            return false;
        }
        return true;
    }

    /// <summary>
    /// 通过命名管道通知已有实例激活主窗口
    /// </summary>
    private static void NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 3000);
            using var writer = new StreamWriter(client);
            writer.WriteLine("ACTIVATE");
            writer.Flush();
        }
        catch (Exception ex)
        {
            // 管道通信失败不阻塞退出
            Log.Warning(ex, "命名管道通知已有实例失败");
        }
    }

    /// <summary>
    /// 启动命名管道监听，接收第二实例的激活请求
    /// </summary>
    private void StartPipeListener()
    {
        var ct = _appCts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync(ct);

                    using var reader = new StreamReader(server);
                    string? message = await reader.ReadLineAsync(ct);

                    if (message == "ACTIVATE")
                    {
                        Log.Information("收到第二实例的激活请求，激活主窗口");
                        ActivateMainWindow();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "命名管道监听异常");
                    // 避免构造失败时紧循环消耗 CPU
                    try { await Task.Delay(1000, ct); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }, ct);
    }

    /// <summary>
    /// 在 UI 线程上激活主窗口（从后台线程、托盘或管道调度）
    /// </summary>
    private void ActivateMainWindow()
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow is not null)
            {
                // 恢复窗口可见（从隐藏/最小化状态）
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                appWindow.Show();

                PInvoke.ShowWindow(hwnd);
                PInvoke.SetForegroundWindow(hwnd);
            }
        });
    }

    /// <summary>
    /// 初始化核心服务：配置 + DI + Serilog + 数据库迁移
    /// </summary>
    private static void InitializeCoreServices()
    {
        using var timer = new OperationTimer(Log.ForContext<App>(), "核心服务初始化");

        // 构建配置
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        // 构建 DI 容器
        var services = new ServiceCollection();
        services.AddSingleton(configuration);

        // 按层注册服务
        services.AddDomain();
        services.AddApplication();
        services.AddInfrastructure();
        services.AddPresentation();
        services.AddBackground();

        Services = services.BuildServiceProvider();

        // 配置 ViewModelLocator（供 Frame.Navigate 创建的页面解析 ViewModel）
        ViewModelLocator.Configure(Services);

        // 初始化 Serilog
        var configProvider = Services.GetRequiredService<IAppConfigProvider>();
        InitializeSerilog(configProvider);

        Log.Information("应用启动 | 版本 {AppVersion}", configProvider.AppVersion);
        Log.Information("DI 容器初始化完成");

        // 执行数据库迁移（仅在有待应用迁移时耗时，已迁移库几乎无延迟）
        try
        {
            using var dbTimer = new OperationTimer(Log.ForContext<App>(), "数据库迁移");
            var dbInitializer = Services.GetRequiredService<Launcher.Application.Persistence.IDatabaseInitializer>();
            Task.Run(() => dbInitializer.InitializeAsync()).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库迁移失败");
        }
    }

    /// <summary>
    /// 初始化 Serilog 日志系统。
    /// 三路文件 Sink：主日志、错误日志、下载日志。
    /// </summary>
    private static void InitializeSerilog(IAppConfigProvider configProvider)
    {
        string logDir = configProvider.LogPath;
        string appVersion = configProvider.AppVersion;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("AppVersion", appVersion)
            .Enrich.FromLogContext()
            // 主日志文件：Information 及以上，按天轮转，保留 30 天
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 100_000_000,
                rollOnFileSizeLimit: true,
                restrictedToMinimumLevel: LogEventLevel.Information)
            // 错误日志：仅 Error + Fatal，保留 90 天
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(logDir, "error-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90,
                restrictedToMinimumLevel: LogEventLevel.Error)
            // 下载模块专用日志：Debug 级别，保留 14 天
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e =>
                    e.Properties.TryGetValue("SourceContext", out var sv)
                    && sv.ToString().Contains("Downloads"))
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    Path.Combine(logDir, "download-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    fileSizeLimitBytes: 200_000_000));

#if DEBUG
        // 开发时控制台输出
        loggerConfig = loggerConfig.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Module}/{Operation}] {Message:lj}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture);
#endif

        Log.Logger = loggerConfig.CreateLogger();
    }
}
