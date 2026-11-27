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
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Posts;

/// <summary>One post of the tab «Посты» of the section «Закладки» (E04-12, FR-PROFILE, ADR-0116).</summary>
/// <param name="PostId">The post the person keeps.</param>
/// <param name="BookmarkedAt">When the person put the bookmark: the section is in this order, newest first.</param>
/// <param name="Card">
/// The post with what its card shows, as a feed gives it; <c>null</c> when the post is no longer published (deleted
/// or taken back to drafts): the section shows a stub in its place.
/// </param>
public sealed record BookmarkedPostView(Guid PostId, DateTimeOffset BookmarkedAt, FeedPostView? Card);

/// <summary>
/// <c>/api/v1/feeds/bookmarks</c> (E04-12, FR-PROFILE, FR-REACT, ADR-0116): the tab «Посты» of the section «Закладки» —
/// the posts the signed-in person keeps (asked of <see cref="IBookmarkList"/>, ADR-0113), in the order the bookmarks
/// were put, newest first, page by page with a cursor (ADR-0006). Only the person's own bookmarks: the route takes no
/// one else's account. A post no longer published stays in its place with no card. The answer is one person's, so it
/// is never cached.
/// </summary>
public sealed class BookmarkFeedEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/feeds").WithTags("Feeds")
            .MapGet("/bookmarks", GetAsync)
            .RequireAuthorization()
            .Produces<CursorPage<BookmarkedPostView>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetBookmarkedPosts")
            .WithSummary("The posts the signed-in person keeps in bookmarks, newest bookmark first, page by page");
    }

    private static async Task<IResult> GetAsync(
        [AsParameters] PageQuery query,
        ClaimsPrincipal user,
        HttpContext http,
        PostsDbContext db,
        IBookmarkList bookmarks,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct)
    {
        if (PostEndpoints.AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var page = query.ToRequest<BookmarkPosition>();
        http.Response.Headers.CacheControl = "private, no-store";

        var entries = await bookmarks.PageAsync(account, BookmarkKind.Post, page.After, page.FetchCount, ct);
        var result = page.ToPage(entries, e => e.Position);
        var ids = result.Items.Select(e => e.TargetId).ToList();
        var published = ids.Count == 0
            ? []
            : await db.Posts.AsNoTracking()
                .Where(p => ids.Contains(p.Id) && p.Status == PostStatus.Published && p.PublishedAt != null)
                .ToListAsync(ct);
        var views = await PostEndpoints.ViewsAsync(published, media, ct);
        var cards = (await FeedPostViews.BuildAsync(views, blogs, comments, ct)).ToDictionary(c => c.Post.Id);
        return TypedResults.Ok(new CursorPage<BookmarkedPostView>(
            [
                .. result.Items.Select(e =>
                    new BookmarkedPostView(e.TargetId, e.BookmarkedAt, cards.GetValueOrDefault(e.TargetId))),
            ],
            result.NextCursor));
    }
}
