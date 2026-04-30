// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Infrastructure;
using Launcher.Infrastructure.Settings;
using Launcher.Shared.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Launcher.Tests.Unit;

public sealed class SettingsServiceFabLibraryConfigTests : IDisposable
{
    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), $"FabSettingsTests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateFabLibraryConfigAsync_PersistsTrueAndFalseAcrossServiceInstances()
    {
        var sut = CreateSut();

        var enableResult = await sut.UpdateFabLibraryConfigAsync(new FabLibraryConfig
        {
            AutoWarmOnStartup = true,
        }, CancellationToken.None);

        enableResult.IsSuccess.Should().BeTrue();
        sut.GetFabLibraryConfig().AutoWarmOnStartup.Should().BeTrue();
        CreateSut().GetFabLibraryConfig().AutoWarmOnStartup.Should().BeTrue();

        var secondSut = CreateSut();
        var disableResult = await secondSut.UpdateFabLibraryConfigAsync(new FabLibraryConfig
        {
            AutoWarmOnStartup = false,
        }, CancellationToken.None);

        disableResult.IsSuccess.Should().BeTrue();
        CreateSut().GetFabLibraryConfig().AutoWarmOnStartup.Should().BeFalse();
    }

    [Fact]
    public async Task ResetToDefaultsAsync_ResetsFabLibraryConfigAndRaisesConfigChanged()
    {
        var sut = CreateSut();
        await sut.UpdateFabLibraryConfigAsync(new FabLibraryConfig
        {
            AutoWarmOnStartup = true,
        }, CancellationToken.None);

        ConfigChangedEvent? fabLibraryChangedEvent = null;
        sut.ConfigChanged += evt =>
        {
            if (evt.Section == "FabLibrary")
            {
                fabLibraryChangedEvent = evt;
            }
        };

        var result = await sut.ResetToDefaultsAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sut.GetFabLibraryConfig().AutoWarmOnStartup.Should().BeFalse();
        fabLibraryChangedEvent.Should().NotBeNull();
        fabLibraryChangedEvent!.Section.Should().Be("FabLibrary");
        fabLibraryChangedEvent.NewConfig.Should().BeOfType<FabLibraryConfig>();
        ((FabLibraryConfig)fabLibraryChangedEvent.NewConfig).AutoWarmOnStartup.Should().BeFalse();
        CreateSut().GetFabLibraryConfig().AutoWarmOnStartup.Should().BeFalse();
    }

    [Fact]
    public void AppConfigProvider_DownloadOptions_UsesConfiguredValues()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Downloads:MaxConcurrentTasks"] = "7",
            ["Downloads:MaxConcurrentChunksPerTask"] = "8",
            ["Downloads:ChunkSizeBytes"] = "20971520",
            ["Downloads:MaxRetryAttempts"] = "6",
            ["Downloads:CheckpointIntervalSeconds"] = "45",
        });

        var options = provider.GetRequiredService<IDownloadOptionsProvider>().DownloadOptions;
        var configProvider = provider.GetRequiredService<IAppConfigProvider>();

        options.MaxConcurrentTasks.Should().Be(7);
        options.MaxConcurrentChunksPerTask.Should().Be(8);
        options.ChunkSizeBytes.Should().Be(20 * 1024 * 1024);
        options.MaxRetryAttempts.Should().Be(6);
        options.CheckpointInterval.Should().Be(TimeSpan.FromSeconds(45));
        configProvider.MaxConcurrentDownloads.Should().Be(7);
        configProvider.MaxChunksPerDownload.Should().Be(8);
    }

    [Fact]
    public void AppConfigProvider_DownloadOptions_UsesDocumentedDefaults()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>());

        var options = provider.GetRequiredService<IDownloadOptionsProvider>().DownloadOptions;
        var configProvider = provider.GetRequiredService<IAppConfigProvider>();

        options.MaxConcurrentTasks.Should().Be(DownloadOptions.DefaultMaxConcurrentTasks);
        options.MaxConcurrentChunksPerTask.Should().Be(DownloadOptions.DefaultMaxConcurrentChunksPerTask);
        options.ChunkSizeBytes.Should().Be(DownloadOptions.DefaultChunkSizeBytes);
        options.MaxRetryAttempts.Should().Be(DownloadOptions.DefaultMaxRetryAttempts);
        options.CheckpointInterval.Should().Be(DownloadOptions.DefaultCheckpointInterval);
        configProvider.MaxConcurrentDownloads.Should().Be(3);
        configProvider.MaxChunksPerDownload.Should().Be(4);
    }

    private SettingsService CreateSut()
    {
        var configProvider = Substitute.For<IAppConfigProvider>();
        configProvider.DataPath.Returns(_dataPath);

        return new SettingsService(configProvider);
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }
}
