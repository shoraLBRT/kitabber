using System.Net;
using Platform.Caching;
using Platform.Testing.Api;
using Platform.Testing.Containers;
using StackExchange.Redis;

namespace Platform.Api.IntegrationTests;

/// <summary>
/// Acceptance check of E01-10 (#21) on real Redis: rate limit counters live in Redis, so two instances of the API
/// share one limit, and each counter expires with its window. The rules themselves are checked in process
/// (Platform.Api.Tests, RateLimitingTests).
/// </summary>
public sealed class RateLimitingIntegrationTests(RedisFixture redis) : IAsyncLifetime
{
    private string _redis = string.Empty;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _redis = redis.CreateDatabase();
        await RedisFixture.FlushAsync(_redis);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TwoInstancesShareOneLimitInRedis()
    {
        // Both hosts start on the same fixed test clock, so they count into the same window.
        using var first = Instance();
        using var second = Instance();
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await firstClient.GetAsync("/api/v1/anything", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secondClient.GetAsync("/api/v1/anything", Ct)).StatusCode);
        using var rejected = await firstClient.GetAsync("/api/v1/anything", Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);

        await using var connection = await ConnectionMultiplexer.ConnectAsync(_redis);
        var server = connection.GetServer(connection.GetEndPoints()[0]);
        var database = ConfigurationOptions.Parse(_redis).DefaultDatabase ?? 0;
        var keys = server.Keys(database, $"{CacheServiceCollectionExtensions.InstanceName}rate:*").ToList();
        Assert.NotEmpty(keys);
        foreach (var key in keys)
        {
            var ttl = await connection.GetDatabase(database).KeyTimeToLiveAsync(key);
            Assert.True(ttl is { } value && value > TimeSpan.Zero && value <= TimeSpan.FromMinutes(1), $"{key}: {ttl}");
        }
    }

    private PlatformApiFactory Instance() => new PlatformApiFactory()
        .WithSetting(CacheServiceCollectionExtensions.ProviderKey, nameof(CacheProvider.Redis))
        .WithSetting($"ConnectionStrings:{CacheServiceCollectionExtensions.RedisConnectionName}", _redis)
        .WithSetting("RateLimiting:Policies:Default:PermitLimit", "2");
}
