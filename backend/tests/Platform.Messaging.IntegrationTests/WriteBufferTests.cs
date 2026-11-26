using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Platform.Caching;
using Platform.Testing.Containers;

namespace Platform.Messaging.IntegrationTests;

/// <summary>
/// E10-09: the write buffer behind fast counters (<see cref="IWriteBuffer"/>, ADR-0083) — Redis, and memory as its
/// fake. Entries are claimed in batches, a batch whose claimer stopped is claimed again after its lease, and in Redis
/// the entries outlive the process that appended them.
/// </summary>
public sealed class WriteBufferTests(RedisFixture redis)
{
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RedisBufferClaimsBatchesAndRetriesAnAbandonedOne()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var services = Provider(new() { ["ConnectionStrings:Redis"] = redis.ConnectionString }, time);
        await AssertBatches(services.GetRequiredService<IWriteBuffer>(), time);
    }

    [Fact]
    public async Task InMemoryBufferBehavesTheSame()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var services = Provider(new() { ["Cache:Provider"] = "InMemory" }, time);
        await AssertBatches(services.GetRequiredService<IWriteBuffer>(), time);
    }

    [Fact]
    public async Task RedisEntriesOutliveTheProcessThatAppendedThem()
    {
        var name = $"test-{Guid.NewGuid():N}";
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var settings = new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = redis.ConnectionString };
        await using (var before = Provider(settings, time))
        {
            var buffer = before.GetRequiredService<IWriteBuffer>();
            await buffer.AppendAsync(name, "first", Ct);
            await buffer.AppendOnceAsync(name, "mark", TimeSpan.FromMinutes(5), "second", Ct);
        }

        // A new connection, as after a restart: the entries and the mark are still in Redis.
        await using var after = Provider(settings, time);
        var restarted = after.GetRequiredService<IWriteBuffer>();
        Assert.False(await restarted.AppendOnceAsync(name, "mark", TimeSpan.FromMinutes(5), "again", Ct));
        var batch = await restarted.ClaimAsync(name, _lease, Ct);
        Assert.Equal(["first", "second"], batch!.Entries);
        await restarted.CompleteAsync(batch, Ct);
    }

    private static ServiceProvider Provider(Dictionary<string, string?> values, TimeProvider time) =>
        new ServiceCollection()
            .AddSingleton(time)
            .AddPlatformCache(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .BuildServiceProvider();

    private static async Task AssertBatches(IWriteBuffer buffer, FakeTimeProvider time)
    {
        var name = $"test-{Guid.NewGuid():N}";
        Assert.Null(await buffer.ClaimAsync(name, _lease, Ct));

        Assert.True(await buffer.AppendOnceAsync(name, "a", TimeSpan.FromMinutes(5), "1", Ct));
        Assert.False(await buffer.AppendOnceAsync(name, "a", TimeSpan.FromMinutes(5), "1 again", Ct));
        await buffer.AppendAsync(name, "2", Ct);

        // The claimer of the first batch stops without completing it; entries appended after go to a new batch.
        var abandoned = await buffer.ClaimAsync(name, _lease, Ct);
        Assert.Equal(["1", "2"], abandoned!.Entries);
        await buffer.AppendAsync(name, "3", Ct);
        var next = await buffer.ClaimAsync(name, _lease, Ct);
        Assert.Equal(["3"], next!.Entries);
        await buffer.CompleteAsync(next, Ct);
        Assert.Null(await buffer.ClaimAsync(name, _lease, Ct));

        // Once its lease runs out, the abandoned batch is claimed again, whole; completed, it is gone.
        time.Advance(_lease + TimeSpan.FromSeconds(1));
        var retried = await buffer.ClaimAsync(name, _lease, Ct);
        Assert.Equal(abandoned.Id, retried!.Id);
        Assert.Equal(["1", "2"], retried.Entries);
        await buffer.CompleteAsync(retried, Ct);
        time.Advance(_lease + TimeSpan.FromSeconds(1));
        Assert.Null(await buffer.ClaimAsync(name, _lease, Ct));
    }
}
