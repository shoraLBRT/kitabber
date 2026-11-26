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
/// The position in the fresh feed (ADR-0043): the publication time and the id of the last post of a page. The feed is
/// sorted by both, newest first, so the order is total and a page never repeats or skips a post.
/// </summary>
/// <param name="PublishedAt">When the last post of the page was published.</param>
/// <param name="Id">Its id, which breaks ties between posts published at the same moment.</param>
public sealed record FreshFeedCursor(DateTimeOffset PublishedAt, Guid Id);

/// <summary>
/// <c>/api/v1/feeds/fresh</c> (FR-FEED, E08-01, ADR-0043): every published post, newest first, page by page with a
/// cursor (ADR-0006). A post published after the first page was read lands before the cursor, so it is neither
/// repeated nor does it push another post past a page boundary. Drafts and deleted posts are never in the feed. A page
/// read once is given from the cache until a post in the feed changes, for <see cref="FeedPageCache.Lifetime"/> at
/// most (E08-09, ADR-0080).
/// </summary>
public sealed class FreshFeedEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/feeds").WithTags("Feeds")
            .MapGet("/fresh", GetAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("GetFreshFeed")
            .WithSummary("Published posts, newest first, page by page with a cursor");
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
        var page = query.ToRequest<FreshFeedCursor>();
        // The fresh feed always exists, so the read is never null.
        var result = await cache.GetOrReadAsync(
            FeedPageCache.FreshFeed,
            page,
            async token => await ReadAsync(page, db, media, blogs, comments, token),
            ct);
        // The reader's exclusions after the shared cache (ADR-0091).
        return TypedResults.Ok(
            ReaderExclusions.Apply(result!, await ReaderExclusions.ForAsync(user, exclusionSources, ct)));
    }

    private static async Task<CursorPage<FeedPostView>> ReadAsync(
        PageRequest<FreshFeedCursor> page,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct)
    {
        var posts = db.Posts.AsNoTracking().Where(p => p.Status == PostStatus.Published && p.PublishedAt != null);
        if (page.After is { } after)
        {
            // A row value comparison, (published_at, id) < (@at, @id): one index range, at any depth of the feed.
            posts = posts.Where(p => EF.Functions.LessThan(
                ValueTuple.Create(p.PublishedAt!.Value, p.Id), ValueTuple.Create(after.PublishedAt, after.Id)));
        }

        var rows = await posts
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
            .Take(page.FetchCount)
            .ToListAsync(ct);
        var result = page.ToPage(rows, p => new FreshFeedCursor(p.PublishedAt!.Value, p.Id));
        var views = await PostEndpoints.ViewsAsync(result.Items, media, ct);
        return new CursorPage<FeedPostView>(
            await FeedPostViews.BuildAsync(views, blogs, comments, ct), result.NextCursor);
    }
}
