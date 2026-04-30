// Copyright (c) Helsincy. All rights reserved.

using Microsoft.Extensions.Configuration;

namespace Launcher.Infrastructure.Auth;

/// <summary>
/// Epic OAuth 运行时配置。
/// </summary>
internal sealed class EpicOAuthOptions
{
    public const string ClientIdEnvironmentVariable = "HELSINCY_EPIC_OAUTH_CLIENT_ID";
    public const string ClientSecretEnvironmentVariable = "HELSINCY_EPIC_OAUTH_CLIENT_SECRET";
    public const string RedirectUriEnvironmentVariable = "HELSINCY_EPIC_OAUTH_REDIRECT_URI";
    public const string EmbeddedLoginUserAgentEnvironmentVariable = "HELSINCY_EPIC_OAUTH_EMBEDDED_LOGIN_USER_AGENT";

    public const string DefaultRedirectUri = "http://localhost:6780/callback";
    public const string DefaultEmbeddedLoginUserAgent = "EpicGamesLauncher/11.0.1-14907503+++Portal+Release-Live";

    public required string ClientId { get; init; }

    /// <summary>
    /// Epic 公开桌面客户端凭据字段。不要在这里放私人或用户专属 secret。
    /// </summary>
    public required string ClientSecret { get; init; }

    public string RedirectUri { get; init; } = DefaultRedirectUri;

    public string EmbeddedLoginUserAgent { get; init; } = DefaultEmbeddedLoginUserAgent;

    public TimeSpan CallbackTimeout { get; init; } = TimeSpan.FromMinutes(3);

    public static EpicOAuthOptions FromConfiguration(IConfiguration configuration)
    {
        var redirectUri = ReadOverride(configuration, "EpicOAuth:RedirectUri", RedirectUriEnvironmentVariable);
        var embeddedLoginUserAgent = ReadOverride(
            configuration,
            "EpicOAuth:EmbeddedLoginUserAgent",
            EmbeddedLoginUserAgentEnvironmentVariable);

        return new EpicOAuthOptions
        {
            ClientId = ReadOverride(configuration, "EpicOAuth:ClientId", ClientIdEnvironmentVariable)
                ?? throw new InvalidOperationException("EpicOAuth:ClientId not configured"),
            ClientSecret = ReadOverride(configuration, "EpicOAuth:ClientSecret", ClientSecretEnvironmentVariable)
                ?? throw new InvalidOperationException("EpicOAuth:ClientSecret not configured"),
            RedirectUri = redirectUri ?? DefaultRedirectUri,
            EmbeddedLoginUserAgent = embeddedLoginUserAgent ?? DefaultEmbeddedLoginUserAgent,
        };
    }

    private static string? ReadOverride(IConfiguration configuration, string key, string environmentVariable)
    {
        var environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        var configuredValue = configuration[key];
        return string.IsNullOrWhiteSpace(configuredValue) ? null : configuredValue;
    }
}
