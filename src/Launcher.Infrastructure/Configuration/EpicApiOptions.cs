// Copyright (c) Helsincy. All rights reserved.

using Microsoft.Extensions.Configuration;

namespace Launcher.Infrastructure.Configuration;

internal sealed class EpicApiOptions
{
    private static readonly Uri DefaultLibraryBaseAddress = new("https://library-service.live.use1a.on.epicgames.com");
    private static readonly Uri DefaultCatalogBaseAddress = new("https://catalog-public-service-prod06.ol.epicgames.com");
    private static readonly Uri DefaultEngineVersionBaseAddress = new("https://www.unrealengine.com/api");

    public Uri LibraryBaseAddress { get; init; } = DefaultLibraryBaseAddress;
    public Uri CatalogBaseAddress { get; init; } = DefaultCatalogBaseAddress;
    public Uri EngineVersionBaseAddress { get; init; } = DefaultEngineVersionBaseAddress;

    public static EpicApiOptions FromConfiguration(IConfiguration configuration) => new()
    {
        LibraryBaseAddress = ReadBaseAddress(
            configuration["EpicApi:LibraryBaseAddress"],
            DefaultLibraryBaseAddress),
        CatalogBaseAddress = ReadBaseAddress(
            configuration["EpicApi:CatalogBaseAddress"],
            DefaultCatalogBaseAddress),
        EngineVersionBaseAddress = ReadBaseAddress(
            configuration["EpicApi:EngineVersionBaseAddress"],
            DefaultEngineVersionBaseAddress),
    };

    private static Uri ReadBaseAddress(string? configuredValue, Uri fallback) =>
        string.IsNullOrWhiteSpace(configuredValue)
            ? fallback
            : new Uri(configuredValue, UriKind.Absolute);
}
