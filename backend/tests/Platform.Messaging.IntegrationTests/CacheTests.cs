using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Testing.Containers;

namespace Platform.Messaging.IntegrationTests;

/// <summary>The cache behind <see cref="IDistributedCache"/>: Redis, and memory as its fake.</summary>
public sealed class CacheTests(RedisFixture redis)
{
    [Fact]
    public async Task RedisCacheStoresAndExpiresValues()
    {
        var connection = redis.ConnectionString;
        await using var services = Provider(new() { ["ConnectionStrings:Redis"] = connection });
        await AssertStoresAndExpires(services.GetRequiredService<IDistributedCache>());
    }

    [Fact]
    public async Task InMemoryCacheBehavesTheSame()
    {
        await using var services = Provider(new() { ["Cache:Provider"] = "InMemory" });
        await AssertStoresAndExpires(services.GetRequiredService<IDistributedCache>());
    }

    [Fact]
    public void RedisCacheWithoutAConnectionStringIsAClearError()
    {
        var configuration = new ConfigurationBuilder().Build();

        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddPlatformCache(configuration));
        Assert.Contains("ConnectionStrings:Redis", error.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider Provider(Dictionary<string, string?> values) =>
        new ServiceCollection()
            .AddPlatformCache(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .BuildServiceProvider();

    private static async Task AssertStoresAndExpires(IDistributedCache cache)
    {
        var ct = TestContext.Current.CancellationToken;
        var key = $"test:{Guid.NewGuid():N}";
        await cache.SetStringAsync(key, "value", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(1500),
        }, ct);

        Assert.Equal("value", await cache.GetStringAsync(key, ct));
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        Assert.Null(await cache.GetStringAsync(key, ct));
    }
}
