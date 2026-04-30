// Copyright (c) Helsincy. All rights reserved.

using Microsoft.Extensions.Configuration;

namespace Launcher.Infrastructure.Configuration;

internal sealed class FabApiOptions
{
    private static readonly Uri DefaultBaseAddress = new("https://www.fab.com/api");

    public Uri BaseAddress { get; init; } = DefaultBaseAddress;

    public static FabApiOptions FromConfiguration(IConfiguration configuration) => new()
    {
        BaseAddress = ReadBaseAddress(configuration["FabApi:BaseAddress"], DefaultBaseAddress),
    };

    private static Uri ReadBaseAddress(string? configuredValue, Uri fallback) =>
        string.IsNullOrWhiteSpace(configuredValue)
            ? fallback
            : new Uri(configuredValue, UriKind.Absolute);
}
