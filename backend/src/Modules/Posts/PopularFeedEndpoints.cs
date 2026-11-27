using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Posts;

/// <summary>
/// The position in the popular feed (ADR-0086): the hotness score and the id of the last post of a page. The feed is
/// sorted by both, highest first, so the order is total.
/// </summary>
/// <param name="Score">The score of the last post of the page.</param>
/// <param name="Id">Its id, which breaks ties between equal scores.</param>
public sealed record PopularFeedCursor(double Score, Guid Id);

/// <summary>
/// <c>/api/v1/feeds/popular</c> (FR-FEED, E08-05, ADR-0086): published posts by hotness, highest first, page by page
/// with a cursor (ADR-0006). The scores are computed by <see cref="HotnessCalculator"/>; a post published after the
/// last pass enters the feed with the next one. A page read once is given from the cache like the other feeds
/// (ADR-0080).
/// </summary>
public sealed class PopularFeedEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/feeds").WithTags("Feeds")
            .MapGet("/popular", GetAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("GetPopularFeed")
            .WithSummary("Published posts by hotness, highest first, page by page with a cursor");
    }

    private static async Task<Ok<CursorPage<FeedPostView>>> GetAsync(
        [AsParameters] PageQuery query,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        FeedPageCache cache,
        ClaimsPrincipal user,
        IEnumerable<IFeedExclusionSource> exclusionSources,
        CancellationToken ct)
    {
        var page = query.ToRequest<PopularFeedCursor>();
        var position = page.After is { } after
            ? string.Create(
                CultureInfo.InvariantCulture, $"{BitConverter.DoubleToInt64Bits(after.Score)}:{after.Id:N}")
            : null;
        // The popular feed always exists, so the read is never null.
        var result = await cache.GetOrReadAsync(
            FeedPageCache.PopularFeed,
            page.Limit,
            position,
            async token => await ReadAsync(page, db, media, blogs, comments, token),
            ct);
        // The reader's exclusions after the shared cache (ADR-0091).
        return TypedResults.Ok(
            ReaderExclusions.Apply(result!, await ReaderExclusions.ForAsync(user, exclusionSources, ct)));
    }

    private static async Task<CursorPage<FeedPostView>> ReadAsync(
        PageRequest<PopularFeedCursor> page,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct)
    {
        var ranked = db.Hotness.AsNoTracking().Join(
            db.Posts.AsNoTracking().Where(p => p.Status == PostStatus.Published && p.PublishedAt != null && p.RepostOfPostId == null),
            h => h.PostId,
            p => p.Id,
            (h, p) => new { h.Score, Post = p });
        if (page.After is { } after)
        {
            // (score, id) < (@score, @id): one range of the index ix_post_hotness_popular_feed.
            ranked = ranked.Where(r => EF.Functions.LessThan(
                ValueTuple.Create(r.Score, r.Post.Id), ValueTuple.Create(after.Score, after.Id)));
        }

        var rows = await ranked
            .OrderByDescending(r => r.Score).ThenByDescending(r => r.Post.Id)
            .Take(page.FetchCount)
            .Select(r => new Ranked(r.Score, r.Post))
            .ToListAsync(ct);
        var result = page.ToPage(rows, r => new PopularFeedCursor(r.Score, r.Post.Id));
        var views = await PostEndpoints.ViewsAsync([.. result.Items.Select(r => r.Post)], media, ct);
        return new CursorPage<FeedPostView>(
            await FeedPostViews.BuildAsync(views, blogs, comments, ct), result.NextCursor);
    }

    private sealed record Ranked(double Score, Post Post);
}
