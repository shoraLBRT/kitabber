using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Preferences.Contracts;

namespace Platform.Modules.Comments;

/// <summary>
/// <c>/api/v1/comments/blogs/{blogId}</c> (E09-15, FR-COMMENT, ADR-0114): the tab «Комментарии» of a blog and a topic,
/// newest first, page by page with a cursor (ADR-0006). A personal blog lists the comments its owner wrote under any
/// published post; a topic lists every comment — guest ones from Telegram too — under its published posts. Deleted
/// comments are never in it, nor those under a post that is no longer published; the comments of the accounts the
/// signed-in reader blocked (ADR-0091) are left out, so the list is read with the reader's token. A blog that does not
/// exist is 404.
/// </summary>
public sealed class BlogCommentEndpoints : IApiEndpoints
{
    public const string BlogNotFoundCode = "comments.blog_not_found";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/comments").WithTags("Comments")
            .MapGet("/blogs/{blogId:guid}", GetAsync)
            .Produces<CursorPage<BlogCommentView>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetBlogComments")
            .WithSummary("The comments of a blog's owner or under a topic's posts, newest first, page by page");
    }

    private static async Task<IResult> GetAsync(
        Guid blogId,
        [AsParameters] PageQuery query,
        CommentsDbContext db,
        IBlogDirectory blogs,
        IPostDirectory posts,
        ClaimsPrincipal user,
        IBlockList blocks,
        CancellationToken ct)
    {
        var page = query.ToRequest<BlogCommentCursor>();
        if (await blogs.FindAsync(blogId, ct) is not { } blog)
        {
            return ApiProblems.NotFound(BlogNotFoundCode);
        }

        var blocked = CommentEndpoints.AccountOf(user) is { } reader
            ? await blocks.BlockedByAsync(reader, ct)
            : new HashSet<Guid>();
        var source = await SourceAsync(blog, db, posts, blocked, ct);
        if (source is null)
        {
            return TypedResults.Ok(new CursorPage<BlogCommentView>([], null));
        }

        // Batch after batch until the page is full, the comments run out or MaxBatches were looked through.
        var kept = new List<BlogCommentView>();
        var position = page.After;
        var exhausted = false;
        for (var round = 0; round < BlogComments.MaxBatches && kept.Count < page.FetchCount; round++)
        {
            var batch = source;
            if (position is { } after)
            {
                batch = batch.Where(c => EF.Functions.LessThan(
                    ValueTuple.Create(c.CreatedAt, c.Id), ValueTuple.Create(after.CreatedAt, after.Id)));
            }

            var rows = await batch
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                .Take(page.FetchCount)
                .ToListAsync(ct);
            if (rows.Count > 0)
            {
                var published = await posts.PublishedHeadlinesAsync([.. rows.Select(c => c.PostId).Distinct()], ct);
                kept.AddRange(BlogComments.Shown(rows, published, blocked).Take(page.FetchCount - kept.Count));
                position = new BlogCommentCursor(rows[^1].CreatedAt, rows[^1].Id);
            }

            if (rows.Count < page.FetchCount)
            {
                exhausted = true;
                break;
            }
        }

        return TypedResults.Ok(BlogComments.Page(kept, page.Limit, position, exhausted));
    }

    // The comments the tab reads, not deleted; null when there are none to read: a personal blog without an owner or
    // whose owner the reader blocked, a topic without published posts.
    private static async Task<IQueryable<Comment>?> SourceAsync(
        BlogSummary blog, CommentsDbContext db, IPostDirectory posts, IReadOnlySet<Guid> blocked, CancellationToken ct)
    {
        var shown = db.Comments.AsNoTracking().Where(c => c.DeletedAt == null);
        if (blog.Kind == BlogKind.Personal)
        {
            return blog.OwnerAccountId is { } owner && !blocked.Contains(owner)
                ? shown.Where(c => c.Kind == CommentKind.Native && c.AuthorAccountId == owner)
                : null;
        }

        var ids = await posts.PublishedPostIdsAsync(blog.Id, ct);
        return ids.Count > 0 ? shown.Where(c => ids.Contains(c.PostId)) : null;
    }
}
