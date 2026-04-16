// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared.Logging;

namespace Launcher.Tests.Unit;

public class LogSanitizerTests
{
    // ── MaskToken ──

    [Theory]
    [InlineData(null, "***")]
    [InlineData("", "***")]
    [InlineData("short", "***")]
    [InlineData("12345678901", "***")]
    public void MaskToken_ShortOrEmpty_ReturnsStars(string? token, string expected)
    {
        LogSanitizer.MaskToken(token!).Should().Be(expected);
    }

    [Fact]
    public void MaskToken_LongToken_PreservesFirstAndLastFour()
    {
        var token = "abcd1234efgh5678";
        var result = LogSanitizer.MaskToken(token);
        result.Should().Be("abcd...5678");
    }

    // ── SanitizeUrl ──

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void SanitizeUrl_NullOrEmpty_ReturnsEmpty(string? url, string expected)
    {
        LogSanitizer.SanitizeUrl(url!).Should().Be(expected);
    }

    [Fact]
    public void SanitizeUrl_NoSensitiveParams_Unchanged()
    {
        var url = "https://example.com/api?page=1&size=10";
        LogSanitizer.SanitizeUrl(url).Should().Be(url);
    }

    [Theory]
    [InlineData("https://a.com?token=secret123", "https://a.com?token=***")]
    [InlineData("https://a.com?code=abc&name=test", "https://a.com?code=***&name=test")]
    [InlineData("https://a.com?key=K1&secret=S1", "https://a.com?key=***&secret=***")]
    [InlineData("https://a.com?PASSWORD=pw", "https://a.com?PASSWORD=***")]
    public void SanitizeUrl_SensitiveParams_Masked(string url, string expected)
    {
        LogSanitizer.SanitizeUrl(url).Should().Be(expected);
    }

    // ── SanitizeHttpBody ──

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void SanitizeHttpBody_NullOrEmpty_ReturnsEmpty(string? body, string expected)
    {
        LogSanitizer.SanitizeHttpBody(body!).Should().Be(expected);
    }

    [Fact]
    public void SanitizeHttpBody_NoSensitiveFields_Unchanged()
    {
        var body = """{"name":"test","count":42}""";
        LogSanitizer.SanitizeHttpBody(body).Should().Be(body);
    }

    [Fact]
    public void SanitizeHttpBody_SensitiveFields_Masked()
    {
        var body = "{\"access_token\":\"eyJhbGci...\",\"refresh_token\":\"dGVzdA==\",\"user\":\"alice\"}";
        var result = LogSanitizer.SanitizeHttpBody(body);
        result.Should().Contain("\"access_token\":\"***\"");
        result.Should().Contain("\"refresh_token\":\"***\"");
        result.Should().Contain("\"user\":\"alice\"");
    }

    [Fact]
    public void SanitizeHttpBody_LongBody_Truncated()
    {
        var body = new string('a', 300);
        var result = LogSanitizer.SanitizeHttpBody(body, maxLength: 100);
        result.Should().HaveLength(100 + "...[truncated]".Length);
        result.Should().EndWith("...[truncated]");
    }

    [Fact]
    public void SanitizeHttpBody_ExactMaxLength_NotTruncated()
    {
        var body = new string('x', 200);
        var result = LogSanitizer.SanitizeHttpBody(body, maxLength: 200);
        result.Should().Be(body);
    }
}
