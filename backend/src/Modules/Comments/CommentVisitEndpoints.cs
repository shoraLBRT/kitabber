using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Preferences.Contracts;

namespace Platform.Modules.Comments;

/// <summary>
/// The comments of a post that are new to the signed-in account since it last opened them (E09-08, ADR-0109).
/// </summary>
/// <param name="PostId">The post.</param>
/// <param name="PreviousVisitAt">The previous opening; <c>null</c> on the first one, when nothing is new.</param>
/// <param name="NewIds">
/// The comments written after it by others, not deleted and not of an author the account blocked, in the order of the
/// tree; at most 500, the oldest threads first.
/// </param>
public sealed record CommentVisitView(Guid PostId, DateTimeOffset? PreviousVisitAt, IReadOnlyList<Guid> NewIds);

/// <summary>
/// The opening of a post's comments by a signed-in account (E09-08, ADR-0109): it answers which comments are new since
/// the previous opening and moves the opening on to now.
/// </summary>
public sealed class CommentVisitEndpoints : IApiEndpoints
{
    /// <summary>The most new comments one answer names.</summary>
    public const int MaxNewIds = 500;

    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapPost("/posts/{postId:guid}/comments/visit", VisitAsync)
            .WithTags("Comments")
            .RequireAuthorization()
            .Produces<CommentVisitView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("VisitComments")
            .WithSummary("Opens the comments of a published post: the ones new since the previous opening, which moves on");
    }

    private static async Task<IResult> VisitAsync(
        Guid postId,
        ClaimsPrincipal user,
        CommentsDbContext db,
        IPostDirectory posts,
        IBlockList blocks,
        TimeProvider time,
        CancellationToken ct)
    {
        if (CommentEndpoints.AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(CommentEndpoints.PostNotFoundCode);
        }

        var previous = await db.Visits.AsNoTracking()
            .Where(v => v.AccountId == account && v.PostId == postId)
            .Select(v => (DateTimeOffset?)v.VisitedAt)
            .FirstOrDefaultAsync(ct);
        var now = time.GetUtcNow();
        // Two tabs opening at once must not trip over each other: one statement writes the row, and an opening never
        // moves back.
        await db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO comments.comment_visits (account_id, post_id, visited_at) VALUES ({account}, {postId}, {now})
             ON CONFLICT (account_id, post_id)
             DO UPDATE SET visited_at = GREATEST(comment_visits.visited_at, excluded.visited_at)
             """,
            ct);

        if (previous is not { } since)
        {
            return TypedResults.Ok(new CommentVisitView(postId, null, []));
        }

        var blocked = await blocks.BlockedByAsync(account, ct);
        var fresh = await db.Comments.AsNoTracking()
            .Where(c => c.PostId == postId && c.CreatedAt > since && c.DeletedAt == null
                && (c.AuthorAccountId == null || c.AuthorAccountId != account))
            .OrderBy(c => c.Path)
            .Select(c => new { c.Id, c.AuthorAccountId })
            .ToListAsync(ct);
        var ids = fresh
            .Where(c => c.AuthorAccountId is not { } author || !blocked.Contains(author))
            .Select(c => c.Id)
            .Take(MaxNewIds)
            .ToList();
        return TypedResults.Ok(new CommentVisitView(postId, since, ids));
    }
}
