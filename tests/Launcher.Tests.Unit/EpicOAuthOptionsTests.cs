// Copyright (c) Helsincy. All rights reserved.

using Launcher.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;

namespace Launcher.Tests.Unit;

public sealed class EpicOAuthOptionsTests
{
    [Fact]
    public void FromConfiguration_UsesEnvironmentVariablesOverConfiguredValues()
    {
        var originalClientId = Environment.GetEnvironmentVariable(EpicOAuthOptions.ClientIdEnvironmentVariable);
        var originalClientSecret = Environment.GetEnvironmentVariable(EpicOAuthOptions.ClientSecretEnvironmentVariable);
        var originalRedirectUri = Environment.GetEnvironmentVariable(EpicOAuthOptions.RedirectUriEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(EpicOAuthOptions.ClientIdEnvironmentVariable, "env-client-id");
            Environment.SetEnvironmentVariable(EpicOAuthOptions.ClientSecretEnvironmentVariable, "env-client-secret");
            Environment.SetEnvironmentVariable(EpicOAuthOptions.RedirectUriEnvironmentVariable, "http://localhost:9876/callback");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EpicOAuth:ClientId"] = "configured-client-id",
                    ["EpicOAuth:ClientSecret"] = "configured-client-secret",
                    ["EpicOAuth:RedirectUri"] = "http://localhost:6780/callback",
                })
                .Build();

            var options = EpicOAuthOptions.FromConfiguration(configuration);

            options.ClientId.Should().Be("env-client-id");
            options.ClientSecret.Should().Be("env-client-secret");
            options.RedirectUri.Should().Be("http://localhost:9876/callback");
        }
        finally
        {
            Environment.SetEnvironmentVariable(EpicOAuthOptions.ClientIdEnvironmentVariable, originalClientId);
            Environment.SetEnvironmentVariable(EpicOAuthOptions.ClientSecretEnvironmentVariable, originalClientSecret);
            Environment.SetEnvironmentVariable(EpicOAuthOptions.RedirectUriEnvironmentVariable, originalRedirectUri);
        }
    }
}
