// Copyright (c) Helsincy. All rights reserved.

using Launcher.Presentation.Shell;

namespace Launcher.Tests.Unit;

/// <summary>
/// Epic 嵌入式登录桥接辅助单元测试。
/// </summary>
public sealed class EpicLoginWebViewBridgeTests
{
    [Theory]
    [InlineData("https://www.epicgames.com/id/login", true)]
    [InlineData("https://accounts.epicgames.com/login", true)]
    [InlineData("https://subdomain.epicgames.com/path", true)]
    [InlineData("http://www.epicgames.com/id/login", false)]
    [InlineData("https://example.com/login", false)]
    public void IsTrustedEpicUri_ShouldValidateExpectedOrigins(string uriText, bool expected)
    {
        // Arrange
        var uri = new Uri(uriText);

        // Act
        var result = EpicLoginWebViewBridge.IsTrustedEpicUri(uri);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://www.epicgames.com/id/register")]
    [InlineData("https://accounts.epicgames.com/logout")]
    public void IsTrustedExternalLaunchUri_WithEpicHttpsUrl_ShouldSucceed(string uriText)
    {
        // Act
        var result = EpicLoginWebViewBridge.IsTrustedExternalLaunchUri(new Uri(uriText));

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("file:///C:/Windows/system32/calc.exe")]
    public void IsTrustedExternalLaunchUri_WithNonEpicUrl_ShouldFail(string uriText)
    {
        // Act
        var result = EpicLoginWebViewBridge.IsTrustedExternalLaunchUri(new Uri(uriText));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParseMessage_WithExchangeCodePayload_ShouldSucceed()
    {
        // Arrange
        const string rawMessage = """
            {
              "type": "exchange_code",
              "exchangeCode": "exchange-code-123"
            }
            """;

        // Act
        var result = EpicLoginWebViewBridge.TryParseMessage(rawMessage, out var message);

        // Assert
        result.Should().BeTrue();
        message.Should().NotBeNull();
        message!.Type.Should().Be("exchange_code");
        message.ExchangeCode.Should().Be("exchange-code-123");
    }

    [Fact]
    public void IDialogService_ShouldNotExposeEpicExchangeCodeLogin()
    {
        // Act
        var method = typeof(IDialogService).GetMethod("ShowEpicExchangeCodeLoginAsync");

        // Assert
        method.Should().BeNull();
    }

    [Fact]
    public void IEpicExchangeCodeLoginDialogService_ShouldExposeEpicExchangeCodeLogin()
    {
        // Act
        var method = typeof(IEpicExchangeCodeLoginDialogService).GetMethod("ShowEpicExchangeCodeLoginAsync");

        // Assert
        method.Should().NotBeNull();
    }
}
