using System.Reflection;
using Platform.Caching;
using Platform.Configuration;
using Platform.Http.RateLimiting;
using Platform.Observability;

namespace Platform.Api;

/// <summary>
/// The settings the API host checks at start (ADR-0007): those of every library the host registers. A module adds
/// its settings here together with its registration; each one is described in docs/CONFIG.md.
/// </summary>
public static class ApiSettings
{
    public static IReadOnlyList<Setting> All { get; } = [.. ObservabilitySettings.All, .. CacheSettings.All, .. RateLimitingSettings.All];

    /// <summary>
    /// The build writes the OpenAPI document by running the entry point under <c>GetDocument.Insider</c> (ADR-0006),
    /// with no stand settings and no Redis. There the cache and the rate limit counters live in memory; the host
    /// never serves a request.
    /// </summary>
    public static WebApplicationBuilder UseInMemoryStoresForOpenApiBuild(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "GetDocument.Insider", StringComparison.Ordinal))
        {
            builder.Configuration[CacheServiceCollectionExtensions.ProviderKey] = nameof(CacheProvider.InMemory);
        }

        return builder;
    }
}
