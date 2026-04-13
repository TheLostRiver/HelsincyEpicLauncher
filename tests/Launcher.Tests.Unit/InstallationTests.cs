// Copyright (c) Helsincy. All rights reserved.

using FluentAssertions;
using Launcher.Domain.Installations;

namespace Launcher.Tests.Unit;

public sealed class InstallationTests
{
    [Fact]
    public void NewInstallation_HasCorrectDefaults()
    {
        var install = new Installation("asset-1", "Test Asset", "1.0.0", @"C:\Games\Test");

        install.AssetId.Should().Be("asset-1");
        install.AssetName.Should().Be("Test Asset");
        install.Version.Should().Be("1.0.0");
        install.InstallPath.Should().Be(@"C:\Games\Test");
        install.AssetType.Should().Be("FabAsset");
        install.State.Should().Be(InstallState.NotInstalled);
        install.SizeBytes.Should().Be(0);
        install.LastError.Should().BeNull();
        install.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RestoredInstallation_HasCorrectState()
    {
        var install = new Installation(
            "id-1", "asset-1", "Test", "2.0.0", @"C:\Games\Test",
            1024 * 1024, "FabAsset", InstallState.Installed,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        install.Id.Should().Be("id-1");
        install.State.Should().Be(InstallState.Installed);
        install.SizeBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public void TransitionTo_ValidTransition_Succeeds()
    {
        var install = new Installation("asset-1", "Test", "1.0.0", @"C:\Games\Test");
        var result = install.TransitionTo(InstallState.Installing);
        result.IsSuccess.Should().BeTrue();
        install.State.Should().Be(InstallState.Installing);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_Fails()
    {
        var install = new Installation("asset-1", "Test", "1.0.0", @"C:\Games\Test");
        var result = install.TransitionTo(InstallState.Installed);
        result.IsSuccess.Should().BeFalse();
        install.State.Should().Be(InstallState.NotInstalled);
    }

    [Fact]
    public void TransitionTo_UpdatesTimestamp()
    {
        var install = new Installation("asset-1", "Test", "1.0.0", @"C:\Games\Test");
        var before = install.UpdatedAt;
        install.TransitionTo(InstallState.Installing);
        install.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void CanTransitionTo_Works()
    {
        var install = new Installation("asset-1", "Test", "1.0.0", @"C:\Games\Test");
        install.CanTransitionTo(InstallState.Installing).Should().BeTrue();
        install.CanTransitionTo(InstallState.Installed).Should().BeFalse();
    }

    [Fact]
    public void SetSize_UpdatesSizeAndTimestamp()
    {
        var install = new Installation("asset-1", "Test", "1.0.0", @"C:\Games\Test");
        install.SetSize(5000);
        install.SizeBytes.Should().Be(5000);
    }

    [Fact]
    public void SetError_And_ClearError()
    {
        var install = new Installation("asset-1", "Test", "1.0.0", @"C:\Games\Test");
        install.SetError("something broke");
        install.LastError.Should().Be("something broke");

        install.ClearError();
        install.LastError.Should().BeNull();
    }
}
