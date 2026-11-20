using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Caching;

/// <summary>Readiness of the cache: one read of a key through <see cref="IDistributedCache"/>.</summary>
internal sealed class CacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    private const string _probeKey = "health:probe";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.GetAsync(_probeKey, cancellationToken);
            return HealthCheckResult.Healthy();
        }
#pragma warning disable CA1031 // Any failure of the store means the cache is not ready.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("the cache does not answer", exception);
        }
    }
}
