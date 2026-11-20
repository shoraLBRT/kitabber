using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Platform.Caching;

/// <summary>Where <see cref="IDistributedCache"/> keeps its data: <c>Cache:Provider</c> in configuration.</summary>
public enum CacheProvider
{
    /// <summary>Redis from <c>ConnectionStrings:Redis</c> (StackExchange.Redis format); the default.</summary>
    Redis,

    /// <summary>Process memory, for tests: not shared between instances.</summary>
    InMemory,
}

public static class CacheServiceCollectionExtensions
{
    public const string ProviderKey = "Cache:Provider";
    public const string RedisConnectionName = "Redis";

    /// <summary>The tag of readiness checks, the same in every library and in the Api's /health/ready.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Prefix of every key, so the platform can share a Redis with other tools.</summary>
    public const string InstanceName = "platform:";

    /// <summary>
    /// Registers <see cref="IDistributedCache"/> (ADR-0005) and <see cref="IRateCounter"/> (ADR-0016): Redis, or memory
    /// when <c>Cache:Provider</c> is <c>InMemory</c>. Modules depend on the interfaces only. A second call (a host
    /// and a module both register the cache) changes nothing.
    /// </summary>
    public static IServiceCollection AddPlatformCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IRateCounter)))
        {
            return services;
        }

        var value = configuration[ProviderKey];
        var provider = CacheProvider.Redis;
        if (!string.IsNullOrWhiteSpace(value)
            && !(Enum.TryParse(value, ignoreCase: true, out provider) && Enum.IsDefined(provider)))
        {
            throw new InvalidOperationException(
                $"{ProviderKey} = '{value}': expected one of {string.Join(", ", Enum.GetNames<CacheProvider>())}.");
        }

        // Readiness (ADR-0015): the host is not ready while the cache does not answer.
        services.AddHealthChecks().AddCheck<CacheHealthCheck>("cache", tags: [ReadyTag]);

        if (provider == CacheProvider.InMemory)
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<IRateCounter, InMemoryRateCounter>();
            return services.AddDistributedMemoryCache();
        }

        var connection = configuration.GetConnectionString(RedisConnectionName);
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{RedisConnectionName} is not set: give host:port,password=... " +
                $"or set {ProviderKey} = InMemory.");
        }

        // AbortOnConnectFail = false: the host starts while Redis is down and reconnects; readiness reports it.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connection);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IRateCounter, RedisRateCounter>();
        return services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connection;
            options.InstanceName = InstanceName;
        });
    }
}
