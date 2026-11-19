using Platform.Configuration;

namespace Platform.Caching;

/// <summary>The settings <see cref="CacheServiceCollectionExtensions.AddPlatformCache"/> reads (docs/CONFIG.md).</summary>
public static class CacheSettings
{
    public static IReadOnlyList<Setting> All { get; } =
    [
        new(CacheServiceCollectionExtensions.ProviderKey, "cache store: Redis, or InMemory for tests")
        {
            Default = nameof(CacheProvider.Redis),
            AllowedValues = Enum.GetNames<CacheProvider>(),
        },
        new($"ConnectionStrings:{CacheServiceCollectionExtensions.RedisConnectionName}",
            "Redis in StackExchange.Redis format: host:port,password=...")
        {
            Secret = true,
            RequiredWhen = configuration => !string.Equals(
                configuration[CacheServiceCollectionExtensions.ProviderKey],
                nameof(CacheProvider.InMemory),
                StringComparison.OrdinalIgnoreCase),
        },
    ];
}
