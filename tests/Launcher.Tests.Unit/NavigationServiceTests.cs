// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Shell.Navigation;
using NSubstitute;

namespace Launcher.Tests.Unit;

/// <summary>
/// INavigationService 相关单元测试
/// </summary>
public class NavigationServiceTests
{
    [Fact]
    public async Task NavigateAsync_Should_UpdateCurrentRoute()
    {
        // Arrange — 使用 NSubstitute mock 验证接口契约
        var navService = Substitute.For<INavigationService>();
        navService.CurrentRoute.Returns("FabLibrary");

        // Act
        await navService.NavigateAsync("FabLibrary");

        // Assert
        navService.CurrentRoute.Should().Be("FabLibrary");
        await navService.Received(1).NavigateAsync("FabLibrary");
    }

    [Fact]
    public async Task GoBackAsync_Should_BeCallable()
    {
        // Arrange
        var navService = Substitute.For<INavigationService>();
        navService.CanGoBack.Returns(true);

        // Act
        await navService.GoBackAsync();

        // Assert
        navService.CanGoBack.Should().BeTrue();
        await navService.Received(1).GoBackAsync();
    }

    [Fact]
    public void Mock_NavigationService_CanGoBack_DefaultFalse()
    {
        // Arrange
        var navService = Substitute.For<INavigationService>();

        // Assert — 默认无导航历史
        navService.CanGoBack.Should().BeFalse();
        navService.CurrentRoute.Should().BeNullOrEmpty();
    }
}
