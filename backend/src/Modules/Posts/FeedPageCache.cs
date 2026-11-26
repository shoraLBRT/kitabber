using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Platform.Http.Pagination;

namespace Platform.Modules.Posts;

/// <summary>
/// The pages of the feeds in the cache (E08-09, ADR-0080): the fresh feed and the feed of a blog. A page read once is
/// given from the cache, not from the database, until it is <see cref="Lifetime"/> old or a change of a post in a feed
/// drops every page with <see cref="InvalidateAsync"/>. Pages are keyed by a generation, a random value stored in the
/// cache; dropping the pages is writing a new generation, so no page has to be listed or deleted one by one. The cache
/// only speeds the feeds up: when it does not answer, the page is read from the database.
/// </summary>
public sealed partial class FeedPageCache(IDistributedCache cache, TimeProvider time, ILogger<FeedPageCache> logger)
{
    /// <summary>
    /// How long a page is given from the cache at most: what other modules show on a card (the best comment, the
    /// blog's name and avatar) and the view count are this late at most (ADR-0080).
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    /// <summary>The key of the current generation; the keys of the pages start with <c>feeds:page:</c>.</summary>
    public const string GenerationKey = "feeds:generation";

    /// <summary>The name of the fresh feed in the keys of its pages.</summary>
    public const string FreshFeed = "fresh";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>The name of the feed of one blog in the keys of its pages.</summary>
    public static string BlogFeed(Guid blogId) => $"blog:{blogId:N}";

    /// <summary>
    /// The page of <paramref name="page"/> of <paramref name="feed"/> from the cache, or <paramref name="read"/> from
    /// the database, which is then cached; a <c>null</c> read (the feed does not exist) is not cached. The generation is
    /// settled before the database is read, so a page read before a change is never cached under the generation that
    /// follows the change.
    /// </summary>
    public async Task<CursorPage<FeedPostView>?> GetOrReadAsync(
        string feed,
        PageRequest<FreshFeedCursor> page,
        Func<CancellationToken, Task<CursorPage<FeedPostView>?>> read,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(feed);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(read);

        string? key = null;
        try
        {
            key = PageKey(await GenerationAsync(ct), feed, page);
            if (await cache.GetAsync(key, ct) is { } bytes
                && JsonSerializer.Deserialize<StoredPage>(bytes, _json) is { } stored
                && stored.ExpiresAt > time.GetUtcNow())
            {
                return stored.Page;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCacheFailed(logger, exception);
            key = null;
        }

        var result = await read(ct);
        if (key is not null && result is not null)
        {
            try
            {
                var entry = JsonSerializer.SerializeToUtf8Bytes(new StoredPage(time.GetUtcNow() + Lifetime, result), _json);
                await cache.SetAsync(
                    key, entry, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime }, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCacheFailed(logger, exception);
            }
        }

        return result;
    }

    /// <summary>
    /// Drops every cached page of every feed. Called after a change of a post that the feeds show (publishing, editing
    /// a published post, deleting it) is saved.
    /// </summary>
    public async Task InvalidateAsync(CancellationToken ct)
    {
        try
        {
            await cache.SetStringAsync(GenerationKey, NewGeneration(), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The pages cached before the change stay at most their lifetime.
            LogCacheFailed(logger, exception);
        }
    }

    private async Task<string> GenerationAsync(CancellationToken ct)
    {
        if (await cache.GetStringAsync(GenerationKey, ct) is { Length: > 0 } current)
        {
            return current;
        }

        // No generation yet (a new or emptied cache): start one before reading the database.
        var generation = NewGeneration();
        await cache.SetStringAsync(GenerationKey, generation, ct);
        return generation;
    }

    private static string NewGeneration() => Guid.NewGuid().ToString("N");

    private static string PageKey(string generation, string feed, PageRequest<FreshFeedCursor> page) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"feeds:page:{generation}:{feed}:{page.Limit}:{(page.After is { } after ? $"{after.PublishedAt.UtcTicks}:{after.Id:N}" : "top")}");

    [LoggerMessage(Level = LogLevel.Warning, Message = "The feed cache did not answer; the page is read from the database")]
    private static partial void LogCacheFailed(ILogger logger, Exception exception);

    private sealed record StoredPage(DateTimeOffset ExpiresAt, CursorPage<FeedPostView> Page);
}
