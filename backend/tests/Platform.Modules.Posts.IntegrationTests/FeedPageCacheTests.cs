using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Platform.Http.Pagination;
using Platform.Testing.Time;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E08-09: the rules of <see cref="FeedPageCache"/> without Docker (ADR-0080) — a page is read once, each feed has its
/// own pages, a change drops them all, a missing feed is not cached, and a cache that does not answer only makes every
/// read go to the database.
/// </summary>
public sealed class FeedPageCacheTests
{
    private static readonly PageRequest<FreshFeedCursor> _top = new PageQuery(null, null).ToRequest<FreshFeedCursor>();

    private int _reads;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PageIsReadOnceAndAgainAfterAChange()
    {
        var cache = MemoryCache();

        await cache.GetOrReadAsync(FeedPageCache.FreshFeed, _top, ReadAsync, Ct);
        await cache.GetOrReadAsync(FeedPageCache.FreshFeed, _top, ReadAsync, Ct);
        Assert.Equal(1, _reads);

        await cache.InvalidateAsync(Ct);
        await cache.GetOrReadAsync(FeedPageCache.FreshFeed, _top, ReadAsync, Ct);
        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task EachFeedHasItsOwnPages()
    {
        var cache = MemoryCache();

        await cache.GetOrReadAsync(FeedPageCache.FreshFeed, _top, ReadAsync, Ct);
        await cache.GetOrReadAsync(FeedPageCache.BlogFeed(Guid.NewGuid()), _top, ReadAsync, Ct);

        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task MissingFeedIsNotCached()
    {
        var cache = MemoryCache();
        var feed = FeedPageCache.BlogFeed(Guid.NewGuid());

        var missing = await cache.GetOrReadAsync(feed, _top, ReadMissingAsync, Ct);
        await cache.GetOrReadAsync(feed, _top, ReadAsync, Ct);

        Assert.Null(missing);
        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task BrokenCacheSendsEveryReadToTheDatabase()
    {
        var cache = new FeedPageCache(new BrokenCache(), TestClock.Create(), NullLogger<FeedPageCache>.Instance);

        var page = await cache.GetOrReadAsync(FeedPageCache.FreshFeed, _top, ReadAsync, Ct);
        await cache.GetOrReadAsync(FeedPageCache.FreshFeed, _top, ReadAsync, Ct);
        await cache.InvalidateAsync(Ct);

        Assert.Equal(2, _reads);
        Assert.Empty(page!.Items);
    }

    private static FeedPageCache MemoryCache() => new(
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
        TestClock.Create(),
        NullLogger<FeedPageCache>.Instance);

    private Task<CursorPage<FeedPostView>?> ReadAsync(CancellationToken ct)
    {
        _reads++;
        return Task.FromResult<CursorPage<FeedPostView>?>(new CursorPage<FeedPostView>([], null));
    }

    private Task<CursorPage<FeedPostView>?> ReadMissingAsync(CancellationToken ct)
    {
        _reads++;
        return Task.FromResult<CursorPage<FeedPostView>?>(null);
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
