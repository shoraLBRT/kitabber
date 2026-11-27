using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Preferences.Contracts;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Comments;

/// <summary>
/// <c>/api/v1/comments/popular</c> (E08-12, FR-FEED, ADR-0105): the widget «Популярные комментарии» of the desktop's
/// right column — the native comments of the last day under published posts with the highest rating. Guest comments
/// carried over from Telegram (FR-TG-6) never enter it. The choice is the same for everyone and is given from the cache
/// (<see cref="PopularCommentsCache"/>); the comments of the accounts the reader blocked are left out after it.
/// </summary>
public sealed class PopularCommentEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/comments").WithTags("Comments")
            .MapGet("/popular", GetAsync)
            .Produces<PopularCommentsView>()
            .ProducesValidationProblem()
            .WithName("GetPopularComments")
            .WithSummary("The highest rated native comments of the last day, for the widget of the right column");
    }

    private static async Task<IResult> GetAsync(
        [FromQuery] int? limit,
        CommentsDbContext db,
        IPostDirectory posts,
        IReactionRatings ratings,
        PopularCommentsCache cache,
        TimeProvider time,
        ClaimsPrincipal user,
        IBlockList blocks,
        CancellationToken ct)
    {
        if (limit is < 1 or > PopularComments.MaxLimit)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["limit"] = [$"Число комментариев — от 1 до {PopularComments.MaxLimit}."],
            });
        }

        var stock = await cache.GetOrReadAsync(token => ReadAsync(db, posts, ratings, time, token), ct);
        var blocked = CommentEndpoints.AccountOf(user) is { } reader
            ? await blocks.BlockedByAsync(reader, ct)
            : new HashSet<Guid>();
        return TypedResults.Ok(new PopularCommentsView(
            PopularComments.ForReader(stock, blocked, time.GetUtcNow(), limit ?? PopularComments.DefaultLimit)));
    }

    // Four queries: the native comments of the day without their texts, the posts among theirs that are published, the
    // ratings of those posts' comments, and the texts of the chosen ones.
    private static async Task<IReadOnlyList<PopularCommentView>> ReadAsync(
        CommentsDbContext db, IPostDirectory posts, IReactionRatings ratings, TimeProvider time, CancellationToken ct)
    {
        var since = time.GetUtcNow() - PopularComments.Window;
        var candidates = await db.Comments.AsNoTracking()
            .Where(c => c.Kind == CommentKind.Native && c.DeletedAt == null && c.CreatedAt >= since)
            .Select(c => new PopularCandidate(c.Id, c.PostId, c.AuthorAccountId!.Value, c.CreatedAt))
            .ToListAsync(ct);
        if (candidates.Count == 0)
        {
            return [];
        }

        var published = await posts.PublishedHeadlinesAsync([.. candidates.Select(c => c.PostId).Distinct()], ct);
        if (published.Count == 0)
        {
            return [];
        }

        var rated = await ratings.CommentRatingsAsync([.. published.Keys], ct);
        var chosen = PopularComments.Pick(
            candidates.Where(c => published.ContainsKey(c.PostId)), rated, PopularComments.Stock);
        if (chosen.Count == 0)
        {
            return [];
        }

        var ids = chosen.Select(c => c.Comment.Id).ToList();
        var texts = await db.Comments.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.DeletedAt == null)
            .Select(c => new { c.Id, c.Text })
            .ToDictionaryAsync(c => c.Id, c => c.Text, ct);

        // A chosen comment deleted between the queries is left out.
        return
        [
            .. chosen
                .Where(c => texts.ContainsKey(c.Comment.Id))
                .Select(c =>
                {
                    var post = published[c.Comment.PostId];
                    return new PopularCommentView(
                        c.Comment.Id,
                        post.Id,
                        post.BlogId,
                        post.Title,
                        post.Slug,
                        c.Comment.AuthorAccountId,
                        BestComments.Excerpt(texts[c.Comment.Id]),
                        c.Rating,
                        c.Comment.CreatedAt);
                }),
        ];
    }
}

/// <summary>
/// The choice of the widget «Популярные комментарии» in the cache (E08-12, ADR-0105): read once, it is given from the
/// cache, not from the database, until it is <see cref="Lifetime"/> old, so a reaction or a new comment shows in the
/// widget that late at most. The cache only speeds the widget up: when it does not answer, the choice is read from the
/// database.
/// </summary>
public sealed partial class PopularCommentsCache(
    IDistributedCache cache, TimeProvider time, ILogger<PopularCommentsCache> logger)
{
    /// <summary>How long a choice is given from the cache at most.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    /// <summary>The key of the cached choice.</summary>
    public const string Key = "comments:popular";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>The cached choice, or <paramref name="read"/> from the database, which is then cached.</summary>
    public async Task<IReadOnlyList<PopularCommentView>> GetOrReadAsync(
        Func<CancellationToken, Task<IReadOnlyList<PopularCommentView>>> read, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(read);
        var cached = true;
        try
        {
            if (await cache.GetAsync(Key, ct) is { } bytes
                && JsonSerializer.Deserialize<Stored>(bytes, _json) is { } stored
                && stored.ExpiresAt > time.GetUtcNow())
            {
                return stored.Comments;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCacheFailed(logger, exception);
            cached = false;
        }

        var result = await read(ct);
        if (cached)
        {
            try
            {
                var entry = JsonSerializer.SerializeToUtf8Bytes(new Stored(time.GetUtcNow() + Lifetime, result), _json);
                await cache.SetAsync(
                    Key, entry, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime }, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCacheFailed(logger, exception);
            }
        }

        return result;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The cache of the popular comments did not answer; they are read from the database")]
    private static partial void LogCacheFailed(ILogger logger, Exception exception);

    private sealed record Stored(DateTimeOffset ExpiresAt, IReadOnlyList<PopularCommentView> Comments);
}
