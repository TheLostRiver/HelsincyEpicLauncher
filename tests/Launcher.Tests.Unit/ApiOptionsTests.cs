// Copyright (c) Helsincy. All rights reserved.

using Launcher.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Launcher.Tests.Unit;

public sealed class ApiOptionsTests
{
    [Fact]
    public void AddInfrastructure_UsesConfiguredApiBaseAddresses()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["FabApi:BaseAddress"] = "https://api.example.test/fab/",
            ["EpicApi:LibraryBaseAddress"] = "https://api.example.test/library/",
            ["EpicApi:CatalogBaseAddress"] = "https://api.example.test/catalog/",
            ["EpicApi:EngineVersionBaseAddress"] = "https://api.example.test/engine/",
            ["UpdateApi:BaseAddress"] = "https://api.example.test/github/",
        });

        var factory = provider.GetRequiredService<IHttpClientFactory>();

        factory.CreateClient("FabApi").BaseAddress.Should().Be(new Uri("https://api.example.test/fab/"));
        factory.CreateClient("EpicLibraryApi").BaseAddress.Should().Be(new Uri("https://api.example.test/library/"));
        factory.CreateClient("EpicCatalogApi").BaseAddress.Should().Be(new Uri("https://api.example.test/catalog/"));
        factory.CreateClient("EngineVersionApi").BaseAddress.Should().Be(new Uri("https://api.example.test/engine/"));
        factory.CreateClient("UpdateApi").BaseAddress.Should().Be(new Uri("https://api.example.test/github/"));
    }

    [Fact]
    public void AddInfrastructure_RejectsNonHttpsApiBaseAddressWithoutLeakingSensitiveQuery()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["UpdateApi:BaseAddress"] = "http://api.example.test/releases?token=super-secret&client_secret=hidden",
        });
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient("UpdateApi"));

        exception.Message.Should().Contain("API 端点必须使用 HTTPS");
        exception.Message.Should().Contain("http://api.example.test/releases");
        exception.Message.Should().NotContain("super-secret");
        exception.Message.Should().NotContain("token=");
        exception.Message.Should().NotContain("client_secret=");
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
