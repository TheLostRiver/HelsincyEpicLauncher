// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Infrastructure.Settings;
using Launcher.Shared.Configuration;

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

    private SettingsService CreateSut()
    {
        var configProvider = Substitute.For<IAppConfigProvider>();
        configProvider.DataPath.Returns(_dataPath);

        return new SettingsService(configProvider);
    }
}