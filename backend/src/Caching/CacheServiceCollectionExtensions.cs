using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>Prefix of every key, so the platform can share a Redis with other tools.</summary>
    public const string InstanceName = "platform:";

    /// <summary>
    /// Registers <see cref="IDistributedCache"/> (ADR-0005): Redis, or memory when <c>Cache:Provider</c> is
    /// <c>InMemory</c>. Modules depend on the interface only.
    /// </summary>
    public static IServiceCollection AddPlatformCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[ProviderKey];
        var provider = CacheProvider.Redis;
        if (!string.IsNullOrWhiteSpace(value)
            && !(Enum.TryParse(value, ignoreCase: true, out provider) && Enum.IsDefined(provider)))
        {
            throw new InvalidOperationException(
                $"{ProviderKey} = '{value}': expected one of {string.Join(", ", Enum.GetNames<CacheProvider>())}.");
        }

        if (provider == CacheProvider.InMemory)
        {
            return services.AddDistributedMemoryCache();
        }

        var connection = configuration.GetConnectionString(RedisConnectionName);
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{RedisConnectionName} is not set: give host:port,password=... " +
                $"or set {ProviderKey} = InMemory.");
        }

        return services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connection;
            options.InstanceName = InstanceName;
        });
    }
}
