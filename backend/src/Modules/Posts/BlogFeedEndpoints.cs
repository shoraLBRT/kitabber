using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Posts;

/// <summary>
/// <c>/api/v1/feeds/blogs/{blogId}</c> (FR-TOPIC, E11-01, ADR-0082): the published posts of one blog — a topic or a
/// personal blog — newest first, page by page with the cursor of the fresh feed (ADR-0043). Drafts and deleted posts
/// are never in it; a blog that does not exist is 404. A page read once is given from the cache like a page of the
/// fresh feed (E08-09, ADR-0080).
/// </summary>
public sealed class BlogFeedEndpoints : IApiEndpoints
{
    public const string BlogNotFoundCode = "posts.blog_not_found";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/feeds").WithTags("Feeds")
            .MapGet("/blogs/{blogId:guid}", GetAsync)
            .Produces<CursorPage<FeedPostView>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetBlogFeed")
            .WithSummary("The published posts of one blog or topic, newest first, page by page with a cursor");
    }

    private static async Task<IResult> GetAsync(
        Guid blogId,
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
        // The reader's exclusions after the shared cache (ADR-0091).
        return await cache.GetOrReadAsync(
                FeedPageCache.BlogFeed(blogId),
                page,
                token => ReadAsync(blogId, page, db, media, blogs, comments, token),
                ct) is { } result
            ? TypedResults.Ok(ReaderExclusions.Apply(result, await ReaderExclusions.ForAsync(user, exclusionSources, ct)))
            : (IResult)ApiProblems.NotFound(BlogNotFoundCode);
    }

    // Null when the blog does not exist; such a read is not cached.
    private static async Task<CursorPage<FeedPostView>?> ReadAsync(
        Guid blogId,
        PageRequest<FreshFeedCursor> page,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct)
    {
        if (await blogs.FindAsync(blogId, ct) is null)
        {
            return null;
        }

        var posts = db.Posts.AsNoTracking()
            .Where(p => p.BlogId == blogId && p.Status == PostStatus.Published && p.PublishedAt != null)
            .WithShownOriginals(db);
        if (page.After is { } after)
        {
            posts = posts.Where(p => EF.Functions.LessThan(
                ValueTuple.Create(p.PublishedAt!.Value, p.Id), ValueTuple.Create(after.PublishedAt, after.Id)));
        }

        var rows = await posts
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
            .Take(page.FetchCount)
            .ToListAsync(ct);
        var result = page.ToPage(rows, p => new FreshFeedCursor(p.PublishedAt!.Value, p.Id));
        return new CursorPage<FeedPostView>(
            await FeedPostViews.BuildAsync(result.Items, db, media, blogs, comments, ct), result.NextCursor);
    }
}
