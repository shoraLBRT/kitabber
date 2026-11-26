using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Reactions;

/// <summary>The likes of a post or a comment (FR-REACT).</summary>
/// <param name="Count">How many people like it.</param>
/// <param name="Liked">The signed-in account likes it; <c>false</c> for a guest.</param>
public sealed record LikeView(int Count, bool Liked);

/// <summary>The likes of one comment under a post.</summary>
/// <param name="CommentId">The comment.</param>
/// <param name="Count">How many people like it.</param>
/// <param name="Liked">The signed-in account likes it; <c>false</c> for a guest.</param>
public sealed record CommentLikesView(Guid CommentId, int Count, bool Liked);

/// <summary>The likes of a post and of its comments, for the post page.</summary>
/// <param name="PostId">The post.</param>
/// <param name="Post">The likes of the post itself.</param>
/// <param name="Comments">The comments under it that someone likes; a comment not listed has none.</param>
public sealed record PostLikesView(Guid PostId, LikeView Post, IReadOnlyList<CommentLikesView> Comments);

/// <summary>
/// Likes on published posts and on comments (E10-01, FR-REACT, ADR-0061): a like is put and taken back, both
/// idempotent; no one likes their own content, and a guest comment takes no likes. Liking needs a confirmed email
/// (ADR-0020); reading the counts does not need an account.
/// </summary>
public sealed class LikeEndpoints : IApiEndpoints
{
    public const string PostNotFoundCode = "reactions.post_not_found";
    public const string CommentNotFoundCode = "reactions.comment_not_found";
    public const string OwnContentCode = "reactions.own_content";
    public const string GuestCommentCode = "reactions.guest_comment";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var posts = v1.MapGroup("/posts/{postId:guid}").WithTags("Reactions");
        posts.MapGet("/likes", GetPostLikesAsync)
            .Produces<PostLikesView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetPostLikes")
            .WithSummary("The likes of a published post and of its comments, and which of them the account likes");
        posts.MapPut("/like", LikePostAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<LikeView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("LikePost")
            .WithSummary("Likes a published post of another author; liking it again changes nothing");
        posts.MapDelete("/like", UnlikePostAsync)
            .RequireAuthorization()
            .Produces<LikeView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("UnlikePost")
            .WithSummary("Takes back the like of a post; taking back a like that is not there changes nothing");

        var comments = v1.MapGroup("/comments/{id:guid}").WithTags("Reactions");
        comments.MapPut("/like", LikeCommentAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<LikeView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("LikeComment")
            .WithSummary("Likes a comment of another account; liking it again changes nothing");
        comments.MapDelete("/like", UnlikeCommentAsync)
            .RequireAuthorization()
            .Produces<LikeView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("UnlikeComment")
            .WithSummary("Takes back the like of a comment; taking back a like that is not there changes nothing");
    }

    private static async Task<IResult> GetPostLikesAsync(
        Guid postId, ClaimsPrincipal user, ReactionsDbContext db, IPostDirectory posts, CancellationToken ct)
    {
        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        var account = AccountOf(user);
        var counts = await db.Reactions.AsNoTracking()
            .Where(r => r.PostId == postId && r.Kind == ReactionKind.Like)
            .GroupBy(r => new { r.TargetKind, r.TargetId })
            .Select(g => new
            {
                g.Key.TargetKind,
                g.Key.TargetId,
                Count = g.Count(),
                Liked = account != null && g.Any(r => r.AccountId == account),
            })
            .ToListAsync(ct);
        var post = counts.SingleOrDefault(c => c.TargetKind == ReactionTarget.Post);
        var commentLikes = counts
            .Where(c => c.TargetKind == ReactionTarget.Comment)
            .OrderBy(c => c.TargetId)
            .Select(c => new CommentLikesView(c.TargetId, c.Count, c.Liked))
            .ToList();
        return TypedResults.Ok(new PostLikesView(
            postId, new LikeView(post?.Count ?? 0, post?.Liked ?? false), commentLikes));
    }

    private static async Task<IResult> LikePostAsync(
        Guid postId,
        ClaimsPrincipal user,
        ReactionsDbContext db,
        IPostDirectory posts,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await posts.FindPublishedAsync(postId, ct) is not { } post)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        return Refused(LikeRules.Refusal(account, post.AuthorAccountId))
            ?? await LikeAsync(db, Reaction.Like(ReactionTarget.Post, postId, postId, account, time.GetUtcNow()), ct);
    }

    private static async Task<IResult> UnlikePostAsync(
        Guid postId, ClaimsPrincipal user, ReactionsDbContext db, IPostDirectory posts, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        return await posts.FindPublishedAsync(postId, ct) is null
            ? ApiProblems.NotFound(PostNotFoundCode)
            : await UnlikeAsync(db, ReactionTarget.Post, postId, account, ct);
    }

    private static async Task<IResult> LikeCommentAsync(
        Guid id,
        ClaimsPrincipal user,
        ReactionsDbContext db,
        ICommentDirectory comments,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await comments.FindShownAsync(id, ct) is not { } comment)
        {
            return ApiProblems.NotFound(CommentNotFoundCode);
        }

        return Refused(LikeRules.Refusal(account, comment.AuthorAccountId))
            ?? await LikeAsync(
                db, Reaction.Like(ReactionTarget.Comment, id, comment.PostId, account, time.GetUtcNow()), ct);
    }

    private static async Task<IResult> UnlikeCommentAsync(
        Guid id, ClaimsPrincipal user, ReactionsDbContext db, ICommentDirectory comments, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        return await comments.FindShownAsync(id, ct) is null
            ? ApiProblems.NotFound(CommentNotFoundCode)
            : await UnlikeAsync(db, ReactionTarget.Comment, id, account, ct);
    }

    // The key is the person and the target: a like already there, even one written by a request at the same moment,
    // is the same like (ADR-0061).
    private static async Task<IResult> LikeAsync(ReactionsDbContext db, Reaction like, CancellationToken ct)
    {
        var there = await db.Reactions.AnyAsync(
            r => r.TargetKind == like.TargetKind && r.TargetId == like.TargetId && r.AccountId == like.AccountId, ct);
        if (!there)
        {
            db.Reactions.Add(like);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            })
            {
                db.ChangeTracker.Clear();
            }
        }

        return TypedResults.Ok(await StateAsync(db, like.TargetKind, like.TargetId, like.AccountId, ct));
    }

    private static async Task<IResult> UnlikeAsync(
        ReactionsDbContext db, ReactionTarget target, Guid targetId, Guid account, CancellationToken ct)
    {
        await db.Reactions
            .Where(r => r.TargetKind == target && r.TargetId == targetId && r.AccountId == account)
            .ExecuteDeleteAsync(ct);
        return TypedResults.Ok(await StateAsync(db, target, targetId, account, ct));
    }

    private static async Task<LikeView> StateAsync(
        ReactionsDbContext db, ReactionTarget target, Guid targetId, Guid account, CancellationToken ct)
    {
        var likes = db.Reactions.AsNoTracking()
            .Where(r => r.TargetKind == target && r.TargetId == targetId && r.Kind == ReactionKind.Like);
        return new LikeView(await likes.CountAsync(ct), await likes.AnyAsync(r => r.AccountId == account, ct));
    }

    private static IResult? Refused(LikeRefusal? refusal) => refusal switch
    {
        LikeRefusal.OwnContent => ApiProblems.Forbidden(OwnContentCode, "Свой пост или комментарий оценить нельзя."),
        LikeRefusal.GuestComment => ApiProblems.Forbidden(
            GuestCommentCode, "Комментарий гостя оценить нельзя: у его автора нет аккаунта."),
        _ => null,
    };

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
