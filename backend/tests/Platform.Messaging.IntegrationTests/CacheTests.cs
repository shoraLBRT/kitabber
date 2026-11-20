using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Testing.Containers;

namespace Platform.Messaging.IntegrationTests;

/// <summary>The cache behind <see cref="IDistributedCache"/> and the counters of <see cref="IRateCounter"/>: Redis, and memory as their fake.</summary>
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
    public async Task RedisCounterCountsAndExpires()
    {
        await using var services = Provider(new() { ["ConnectionStrings:Redis"] = redis.ConnectionString });
        await AssertCountsAndExpires(services.GetRequiredService<IRateCounter>(), () => Task.Delay(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task InMemoryCounterBehavesTheSame()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        await using var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(time)
            .AddPlatformCache(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Cache:Provider"] = "InMemory" }).Build())
            .BuildServiceProvider();
        await AssertCountsAndExpires(services.GetRequiredService<IRateCounter>(), () =>
        {
            time.Advance(TimeSpan.FromSeconds(2));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void ASecondRegistrationChangesNothing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cache:Provider"] = "InMemory" }).Build();
        var services = new ServiceCollection().AddPlatformCache(configuration);
        var count = services.Count;

        services.AddPlatformCache(configuration);

        Assert.Equal(count, services.Count);
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

    private static async Task AssertCountsAndExpires(IRateCounter counter, Func<Task> waitPastLifetime)
    {
        var ct = TestContext.Current.CancellationToken;
        var key = $"test:{Guid.NewGuid():N}";
        var lifetime = TimeSpan.FromMilliseconds(1500);

        Assert.Equal(1, await counter.IncrementAsync(key, lifetime, ct));
        Assert.Equal(2, await counter.IncrementAsync(key, lifetime, ct));
        Assert.Equal(3, await counter.IncrementAsync(key, lifetime, ct));
        await waitPastLifetime();
        Assert.Equal(1, await counter.IncrementAsync(key, lifetime, ct));
    }
}
