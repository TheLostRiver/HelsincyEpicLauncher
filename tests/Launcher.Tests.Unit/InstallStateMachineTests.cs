// Copyright (c) Helsincy. All rights reserved.

using FluentAssertions;
using Launcher.Domain.Installations;

namespace Launcher.Tests.Unit;

public sealed class InstallStateMachineTests
{
    // ===== 合法转换 =====

    [Theory]
    [InlineData(InstallState.NotInstalled, InstallState.Installing)]
    [InlineData(InstallState.Installing, InstallState.Installed)]
    [InlineData(InstallState.Installing, InstallState.Failed)]
    [InlineData(InstallState.Installed, InstallState.Verifying)]
    [InlineData(InstallState.Installed, InstallState.Uninstalling)]
    [InlineData(InstallState.Verifying, InstallState.Installed)]
    [InlineData(InstallState.Verifying, InstallState.NeedsRepair)]
    [InlineData(InstallState.Verifying, InstallState.Failed)]
    [InlineData(InstallState.NeedsRepair, InstallState.Repairing)]
    [InlineData(InstallState.NeedsRepair, InstallState.Uninstalling)]
    [InlineData(InstallState.Repairing, InstallState.Installed)]
    [InlineData(InstallState.Repairing, InstallState.Failed)]
    [InlineData(InstallState.Uninstalling, InstallState.NotInstalled)]
    [InlineData(InstallState.Uninstalling, InstallState.Failed)]
    [InlineData(InstallState.Failed, InstallState.Installing)]
    [InlineData(InstallState.Failed, InstallState.Uninstalling)]
    public void ValidTransition_Succeeds(InstallState from, InstallState to)
    {
        var sm = new InstallStateMachine(from);
        var result = sm.TransitionTo(to);
        result.IsSuccess.Should().BeTrue();
        sm.Current.Should().Be(to);
    }

    // ===== 非法转换 =====

    [Theory]
    [InlineData(InstallState.NotInstalled, InstallState.Installed)]
    [InlineData(InstallState.NotInstalled, InstallState.Verifying)]
    [InlineData(InstallState.Installing, InstallState.Verifying)]
    [InlineData(InstallState.Installed, InstallState.Installing)]
    [InlineData(InstallState.Verifying, InstallState.Installing)]
    [InlineData(InstallState.NeedsRepair, InstallState.Installed)]
    [InlineData(InstallState.Repairing, InstallState.NeedsRepair)]
    public void InvalidTransition_Fails(InstallState from, InstallState to)
    {
        var sm = new InstallStateMachine(from);
        var result = sm.TransitionTo(to);
        result.IsSuccess.Should().BeFalse();
        sm.Current.Should().Be(from);
    }

    [Fact]
    public void DefaultConstructor_StartsNotInstalled()
    {
        var sm = new InstallStateMachine();
        sm.Current.Should().Be(InstallState.NotInstalled);
    }

    [Fact]
    public void CanTransitionTo_ReturnsTrue_ForValidTarget()
    {
        var sm = new InstallStateMachine(InstallState.Installed);
        sm.CanTransitionTo(InstallState.Verifying).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_ReturnsFalse_ForInvalidTarget()
    {
        var sm = new InstallStateMachine(InstallState.Installed);
        sm.CanTransitionTo(InstallState.Installing).Should().BeFalse();
    }

    // ===== 完整流程 =====

    [Fact]
    public void HappyPath_NotInstalled_To_Installed()
    {
        var sm = new InstallStateMachine();
        sm.TransitionTo(InstallState.Installing).IsSuccess.Should().BeTrue();
        sm.TransitionTo(InstallState.Installed).IsSuccess.Should().BeTrue();
        sm.Current.Should().Be(InstallState.Installed);
    }

    [Fact]
    public void VerifyAndRepair_Flow()
    {
        var sm = new InstallStateMachine(InstallState.Installed);
        sm.TransitionTo(InstallState.Verifying).IsSuccess.Should().BeTrue();
        sm.TransitionTo(InstallState.NeedsRepair).IsSuccess.Should().BeTrue();
        sm.TransitionTo(InstallState.Repairing).IsSuccess.Should().BeTrue();
        sm.TransitionTo(InstallState.Installed).IsSuccess.Should().BeTrue();
        sm.Current.Should().Be(InstallState.Installed);
    }

    [Fact]
    public void UninstallFlow()
    {
        var sm = new InstallStateMachine(InstallState.Installed);
        sm.TransitionTo(InstallState.Uninstalling).IsSuccess.Should().BeTrue();
        sm.TransitionTo(InstallState.NotInstalled).IsSuccess.Should().BeTrue();
        sm.Current.Should().Be(InstallState.NotInstalled);
    }

    [Fact]
    public void FailedRetry_Flow()
    {
        var sm = new InstallStateMachine(InstallState.NotInstalled);
        sm.TransitionTo(InstallState.Installing).IsSuccess.Should().BeTrue();
        sm.TransitionTo(InstallState.Failed).IsSuccess.Should().BeTrue();
        sm.TransitionTo(InstallState.Installing).IsSuccess.Should().BeTrue();
        sm.TransitionTo(InstallState.Installed).IsSuccess.Should().BeTrue();
        sm.Current.Should().Be(InstallState.Installed);
    }
}
