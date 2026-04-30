// Copyright (c) Helsincy. All rights reserved.

using Microsoft.Extensions.Configuration;

namespace Launcher.Infrastructure.Configuration;

internal sealed class UpdateOptions
{
    private static readonly Uri DefaultBaseAddress = new("https://api.github.com");

    public Uri BaseAddress { get; init; } = DefaultBaseAddress;

    public static UpdateOptions FromConfiguration(IConfiguration configuration) => new()
    {
        BaseAddress = ReadBaseAddress(configuration["UpdateApi:BaseAddress"], DefaultBaseAddress),
    };

    private static Uri ReadBaseAddress(string? configuredValue, Uri fallback) =>
        string.IsNullOrWhiteSpace(configuredValue)
            ? fallback
            : new Uri(configuredValue, UriKind.Absolute);
}
