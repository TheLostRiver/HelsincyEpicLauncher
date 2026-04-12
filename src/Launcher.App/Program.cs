// Copyright (c) Helsincy. All rights reserved.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Launcher.Application;
using Launcher.Background;
using Launcher.Domain;
using Launcher.Infrastructure;
using Launcher.Presentation;
using Launcher.Shared.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Globalization;

namespace Launcher.App;

/// <summary>
/// 应用程序入口点。构建配置 + DI 容器 + Serilog 日志，启动 WinUI 3 应用。
/// </summary>
public static class Program
{
    /// <summary>
    /// 全局服务提供器（供 WinUI 3 App 类访问）
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        // 构建配置
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        // 构建 DI 容器
        var services = new ServiceCollection();

        // 注册配置实例
        services.AddSingleton(configuration);

        // 按层注册服务
        services.AddDomain();
        services.AddApplication();
        services.AddInfrastructure();
        services.AddPresentation();
        services.AddBackground();

        Services = services.BuildServiceProvider();

        // 初始化 Serilog（需要 IAppConfigProvider 获取日志路径）
        var configProvider = Services.GetRequiredService<IAppConfigProvider>();
        InitializeSerilog(configProvider);

        Log.Information("应用启动 | 版本 {AppVersion}", configProvider.AppVersion);

        try
        {
            // 执行数据库迁移
            var dbInitializer = Services.GetRequiredService<Launcher.Application.Persistence.IDatabaseInitializer>();
            dbInitializer.InitializeAsync().GetAwaiter().GetResult();
            Log.Information("数据库迁移执行完成");

            // Task 0.7 中启动 WinUI 3 应用
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用因未处理异常而终止");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// 初始化 Serilog 日志系统。
    /// 配置三个文件 Sink：主日志、错误日志、下载日志。
    /// DEBUG 模式下额外启用控制台输出。
    /// </summary>
    private static void InitializeSerilog(IAppConfigProvider configProvider)
    {
        string logDir = configProvider.LogPath;
        string appVersion = configProvider.AppVersion;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithThreadId()
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
