using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Platform.Http.Pagination;
using Platform.Testing.Time;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E08-09: the rules of <see cref="FreshFeedCache"/> without Docker (ADR-0080) — a page is read once, a change drops
/// it, and a cache that does not answer only makes every read go to the database.
/// </summary>
public sealed class FreshFeedCacheTests
{
    private static readonly PageRequest<FreshFeedCursor> _top = new PageQuery(null, null).ToRequest<FreshFeedCursor>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PageIsReadOnceAndAgainAfterAChange()
    {
        var cache = new FreshFeedCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            TestClock.Create(),
            NullLogger<FreshFeedCache>.Instance);
        var reads = 0;

        await cache.GetOrReadAsync(_top, _ => ReadAsync(ref reads), Ct);
        await cache.GetOrReadAsync(_top, _ => ReadAsync(ref reads), Ct);
        Assert.Equal(1, reads);

        await cache.InvalidateAsync(Ct);
        await cache.GetOrReadAsync(_top, _ => ReadAsync(ref reads), Ct);
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task BrokenCacheSendsEveryReadToTheDatabase()
    {
        var cache = new FreshFeedCache(new BrokenCache(), TestClock.Create(), NullLogger<FreshFeedCache>.Instance);
        var reads = 0;

        var page = await cache.GetOrReadAsync(_top, _ => ReadAsync(ref reads), Ct);
        await cache.GetOrReadAsync(_top, _ => ReadAsync(ref reads), Ct);
        await cache.InvalidateAsync(Ct);

        Assert.Equal(2, reads);
        Assert.Empty(page.Items);
    }

    private static Task<CursorPage<FeedPostView>> ReadAsync(ref int reads)
    {
        reads++;
        return Task.FromResult(new CursorPage<FeedPostView>([], null));
    }

    private sealed class BrokenCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Redis is down.");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Redis is down.");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("Redis is down.");

        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new InvalidOperationException("Redis is down.");

        public void Refresh(string key) => throw new InvalidOperationException("Redis is down.");

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Redis is down.");

        public void Remove(string key) => throw new InvalidOperationException("Redis is down.");

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Redis is down.");
    }
}
