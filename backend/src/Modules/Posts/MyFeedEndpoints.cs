using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
/// <c>/api/v1/feeds/my</c> (FR-FEED, E08-04, ADR-0089): «Моя лента» of the signed-in reader — the published posts of
/// the blogs and topics they are subscribed to (asked of <see cref="IBlogSubscriptions"/>, ADR-0085) and every post of
/// the people whose personal blogs they follow, wherever it is published; newest first, page by page with the cursor
/// of the fresh feed (ADR-0043). What any <see cref="IFeedExclusionSource"/> names for the reader is left out. The feed
/// is one reader's, so it is never put in the shared page cache (ADR-0080) and its answer is not to be cached.
/// </summary>
public sealed class MyFeedEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/feeds").WithTags("Feeds")
            .MapGet("/my", GetAsync)
            .RequireAuthorization()
            .Produces<CursorPage<FeedPostView>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetMyFeed")
            .WithSummary("The published posts of the reader's subscriptions, newest first, page by page with a cursor");
    }

    private static async Task<IResult> GetAsync(
        [AsParameters] PageQuery query,
        ClaimsPrincipal user,
        HttpContext http,
        PostsDbContext db,
        IBlogSubscriptions subscriptions,
        IEnumerable<IFeedExclusionSource> exclusionSources,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct)
    {
        if (PostEndpoints.AccountOf(user) is not { } reader)
        {
            return TypedResults.Unauthorized();
        }

        var page = query.ToRequest<FreshFeedCursor>();
        http.Response.Headers.CacheControl = "private, no-store";

        var subscribed = await subscriptions.SubscribedBlogsAsync(reader, ct);
        if (subscribed.Count == 0)
        {
            return TypedResults.Ok(new CursorPage<FeedPostView>([], null));
        }

        var blogIds = subscribed.Select(b => b.Id).ToList();
        var followed = subscribed
            .Where(b => b.Kind == BlogKind.Personal && b.OwnerAccountId is not null)
            .Select(b => b.OwnerAccountId!.Value)
            .ToList();
        var excluded = await ReaderExclusions.ForAsync(reader, exclusionSources, ct);

        var hiddenPosts = excluded.Posts.ToList();
        var hiddenBlogs = excluded.Blogs.ToList();
        var blocked = excluded.Authors.ToList();
        var posts = db.Posts.AsNoTracking()
            .Where(p => p.Status == PostStatus.Published && p.PublishedAt != null)
            .Where(p => blogIds.Contains(p.BlogId) || followed.Contains(p.AuthorAccountId))
            .Where(p => !hiddenPosts.Contains(p.Id)
                && !hiddenBlogs.Contains(p.BlogId)
                && !blocked.Contains(p.AuthorAccountId))
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
        // The posts are already left out by the query; this drops a blocked author's best comment from a card.
        return TypedResults.Ok(ReaderExclusions.Apply(
            new CursorPage<FeedPostView>(
                await FeedPostViews.BuildAsync(result.Items, db, media, blogs, comments, ct), result.NextCursor),
            excluded));
    }
}
