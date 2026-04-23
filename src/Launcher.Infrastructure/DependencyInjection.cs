// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.Diagnostics.Contracts;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.EngineVersions.Contracts;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Launcher.Application.Modules.Plugins.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Application.Modules.Updates.Contracts;
using Launcher.Application.Persistence;
using Launcher.Infrastructure.Auth;
using Launcher.Infrastructure.Configuration;
using Launcher.Infrastructure.Diagnostics;
using Launcher.Infrastructure.Downloads;
using Launcher.Infrastructure.EngineVersions;
using Launcher.Infrastructure.FabLibrary;
using Launcher.Infrastructure.Installations;
using Launcher.Infrastructure.Network;
using Launcher.Infrastructure.Plugins;
using Launcher.Infrastructure.Persistence.Sqlite;
using Launcher.Infrastructure.Persistence.Sqlite.Migrations;
using Launcher.Infrastructure.Settings;
using Launcher.Infrastructure.Updates;
using Launcher.Shared.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Launcher.Infrastructure;

/// <summary>
/// Infrastructure 层 DI 注册扩展
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // 配置
        services.AddSingleton<IAppConfigProvider, AppConfigProvider>();

        // 用户设置（注册具体类型 + 双接口）
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsCommandService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<ISettingsReadService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<IThemePersistenceService, FileThemePersistenceService>();

        // 数据库
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IMigration, Migration_001_AppSettings>();
        services.AddSingleton<IMigration, Migration_002_Downloads>();
        services.AddSingleton<IMigration, Migration_003_Installations>();
        services.AddSingleton<IMigration, Migration_004_SettingsKv>();
        services.AddSingleton<IMigration, Migration_005_DownloadCheckpoints>();
        services.AddSingleton<IMigration, Migration_006_Indexes>();
        services.AddSingleton<IDatabaseInitializer, MigrationRunner>();

        // 诊断
        services.AddSingleton<IDiagnosticsReadService, DiagnosticsService>();
        services.AddSingleton<ICacheManager, CacheManager>();

        // 认证
        services.AddHttpClient("EpicAuth");
        services.AddSingleton<ITokenStore, FileTokenStore>();
        services.AddSingleton<EpicOAuthHandler>();
        services.AddSingleton<IAuthService, AuthService>();

        // 下载
        services.AddHttpClient("ChunkDownload");
        services.AddSingleton<ChunkDownloadClient>();
        services.AddSingleton<IDownloadTaskRepository, DownloadTaskRepository>();
        services.AddSingleton<IDownloadScheduler, DownloadScheduler>();
        services.AddSingleton<IDownloadOrchestrator, DownloadOrchestrator>();
        services.AddSingleton<DownloadRuntimeStore>();
        services.AddSingleton<IDownloadRuntimeStore>(sp => sp.GetRequiredService<DownloadRuntimeStore>());
        services.AddSingleton<IDownloadCommandService, DownloadCommandService>();
        services.AddSingleton<IDownloadReadService, DownloadReadService>();

        // 安装
        services.AddSingleton<IInstallationRepository, InstallationRepository>();
        services.AddSingleton<InstallWorker>();
        services.AddSingleton<IHashingService, HashingService>();
        services.AddSingleton<IIntegrityVerifier, IntegrityVerifier>();
        services.AddSingleton<IRepairDownloadUrlProvider, RepairDownloadUrlProvider>();
        services.AddSingleton<RepairFileDownloader>();
        services.AddSingleton<IInstallCommandService, InstallCommandService>();
        services.AddSingleton<IInstallReadService, InstallReadService>();

        // Fab 资产库
        services.AddHttpClient("FabApi", client =>
        {
            client.BaseAddress = new Uri("https://www.fab.com/api");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            EnsureHttps(client.BaseAddress);
        });
        services.AddHttpClient("EpicLibraryApi", client =>
        {
            client.BaseAddress = new Uri("https://library-service.live.use1a.on.epicgames.com");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.AcceptEncoding.Clear();
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
            EnsureHttps(client.BaseAddress);
        });
        services.AddHttpClient("EpicCatalogApi", client =>
        {
            client.BaseAddress = new Uri("https://catalog-public-service-prod06.ol.epicgames.com");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.AcceptEncoding.Clear();
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
            EnsureHttps(client.BaseAddress);
        });
        services.AddHttpClient("ThumbnailDownload", client =>
        {
            client.DefaultRequestHeaders.AcceptEncoding.Clear();
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
        });
        services.AddSingleton<FabApiClient>();
        services.AddSingleton<IFabPreviewMetadataResolver>(sp =>
        {
            var listingPageReadService = sp.GetService<IFabListingPageReadService>();
            return listingPageReadService is null
                ? new NullFabPreviewMetadataResolver()
                : new FabListingHtmlPreviewMetadataResolver(listingPageReadService);
        });
        services.AddSingleton<IFabPreviewUrlReadService, FabPreviewUrlReadService>();
        services.AddSingleton<IFabDetailEnrichmentResolver, NullFabDetailEnrichmentResolver>();
        services.AddSingleton<EpicOwnedFabCatalogClient>();
        services.AddSingleton<IFabDownloadInfoProvider, FabDownloadInfoProvider>();
        services.AddSingleton<IThumbnailCacheService>(sp =>
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var configProvider = sp.GetRequiredService<IAppConfigProvider>();
            var cacheDir = Path.Combine(configProvider.CachePath, "Thumbnails");
            return new ThumbnailCacheService(httpFactory.CreateClient("ThumbnailDownload"), cacheDir);
        });
        services.AddSingleton<IFabCatalogReadService, FabCatalogReadService>();
        services.AddSingleton<IFabAssetCommandService, FabAssetCommandService>();

        // 引擎版本
        services.AddHttpClient("EngineVersionApi", client =>
        {
            client.BaseAddress = new Uri("https://www.unrealengine.com/api");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            EnsureHttps(client.BaseAddress);
        });
        services.AddSingleton<EngineVersionApiClient>();
        services.AddSingleton<IEngineVersionReadService, EngineVersionReadService>();
        services.AddSingleton<IEngineVersionCommandService, EngineVersionCommandService>();

        // 插件管理
        services.AddSingleton<IPluginReadService, PluginReadService>();
        services.AddSingleton<IPluginCommandService, PluginCommandService>();

        // 自动更新
        services.AddHttpClient("UpdateApi", client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("User-Agent", Launcher.Shared.AppConstants.AppName);
            EnsureHttps(client.BaseAddress);
        });
        services.AddSingleton<AppUpdateService>();
        services.AddSingleton<IAppUpdateService>(sp => sp.GetRequiredService<AppUpdateService>());
        services.AddSingleton<IInternalUpdateNotifier>(sp => sp.GetRequiredService<AppUpdateService>());

        // 网络监视
        services.AddSingleton<NetworkMonitor>();
        services.AddSingleton<INetworkMonitor>(sp => sp.GetRequiredService<NetworkMonitor>());

        return services;
    }

    private static void EnsureHttps(Uri? baseAddress)
    {
        if (baseAddress is not null && !string.Equals(baseAddress.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"API 端点必须使用 HTTPS: {baseAddress}");
    }
}
