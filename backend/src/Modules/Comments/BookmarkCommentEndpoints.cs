using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Comments;

/// <summary>One comment of the tab «Комментарии» of the section «Закладки» (E04-12, FR-PROFILE, ADR-0116).</summary>
/// <param name="CommentId">The comment the person keeps.</param>
/// <param name="PostId">The post it is under.</param>
/// <param name="BookmarkedAt">When the person put the bookmark: the section is in this order, newest first.</param>
/// <param name="Comment">
/// The comment with its post, as the tab «Комментарии» of a blog gives it (ADR-0114); <c>null</c> when it is no longer
/// shown (deleted, or its post deleted or taken back to drafts): the section shows a stub in its place.
/// </param>
public sealed record BookmarkedCommentView(
    Guid CommentId, Guid PostId, DateTimeOffset BookmarkedAt, BlogCommentView? Comment);

/// <summary>
/// <c>/api/v1/comments/bookmarks</c> (E04-12, FR-PROFILE, FR-REACT, ADR-0116): the tab «Комментарии» of the section
/// «Закладки» — the comments the signed-in person keeps (asked of <see cref="IBookmarkList"/>, ADR-0113), in the order
/// the bookmarks were put, newest first, page by page with a cursor (ADR-0006). Only the person's own bookmarks: the
/// route takes no one else's account. A comment no longer shown stays in its place without its text. The answer is
/// one person's, so it is never cached.
/// </summary>
public sealed class BookmarkCommentEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/comments").WithTags("Comments")
            .MapGet("/bookmarks", GetAsync)
            .RequireAuthorization()
            .Produces<CursorPage<BookmarkedCommentView>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetBookmarkedComments")
            .WithSummary("The comments the signed-in person keeps in bookmarks, newest bookmark first, page by page");
    }

    private static async Task<IResult> GetAsync(
        [AsParameters] PageQuery query,
        ClaimsPrincipal user,
        HttpContext http,
        CommentsDbContext db,
        IBookmarkList bookmarks,
        IPostDirectory posts,
        CancellationToken ct)
    {
        if (CommentEndpoints.AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var page = query.ToRequest<BookmarkPosition>();
        http.Response.Headers.CacheControl = "private, no-store";

        var entries = await bookmarks.PageAsync(account, BookmarkKind.Comment, page.After, page.FetchCount, ct);
        var result = page.ToPage(entries, e => e.Position);
        var ids = result.Items.Select(e => e.TargetId).ToList();
        var shown = ids.Count == 0
            ? []
            : await db.Comments.AsNoTracking().Where(c => ids.Contains(c.Id) && c.DeletedAt == null).ToListAsync(ct);
        var published = shown.Count == 0
            ? new Dictionary<Guid, PostHeadline>()
            : await posts.PublishedHeadlinesAsync([.. shown.Select(c => c.PostId).Distinct()], ct);
        var views = new Dictionary<Guid, BlogCommentView>();
        foreach (var comment in shown)
        {
            if (published.TryGetValue(comment.PostId, out var post))
            {
                views[comment.Id] = BlogComments.View(comment, post);
            }
        }

        return TypedResults.Ok(new CursorPage<BookmarkedCommentView>(
            [
                .. result.Items.Select(e =>
                    new BookmarkedCommentView(e.TargetId, e.PostId, e.BookmarkedAt, views.GetValueOrDefault(e.TargetId))),
            ],
            result.NextCursor));
    }
}
