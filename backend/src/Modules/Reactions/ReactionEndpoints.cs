using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Messaging;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Reactions.Contracts;
using Platform.Realtime;

namespace Platform.Modules.Reactions;

/// <summary>The reactions of a post or a comment (FR-REACT).</summary>
/// <param name="Reactions">Every kind someone put, with its count, in the order of the kinds; a kind nobody put is left out.</param>
/// <param name="Mine">The code of the signed-in account's reaction; <c>null</c> without one and for a guest.</param>
public sealed record ReactionsView(IReadOnlyList<ReactionCountView> Reactions, string? Mine);

/// <summary>The reactions of one comment under a post.</summary>
/// <param name="CommentId">The comment.</param>
/// <param name="Reactions">Every kind someone put, with its count.</param>
/// <param name="Mine">The code of the signed-in account's reaction; <c>null</c> without one and for a guest.</param>
public sealed record CommentReactionsView(Guid CommentId, IReadOnlyList<ReactionCountView> Reactions, string? Mine);

/// <summary>The reactions of a post and of its comments, for the post page.</summary>
/// <param name="PostId">The post.</param>
/// <param name="Post">The reactions of the post itself.</param>
/// <param name="Comments">The comments under it that have any; a comment not listed has none.</param>
public sealed record PostReactionsView(Guid PostId, ReactionsView Post, IReadOnlyList<CommentReactionsView> Comments);

/// <summary>Which reaction to put.</summary>
/// <param name="Kind">The code of a reaction the set offers (<c>GET /api/v1/reactions/set</c>).</param>
public sealed record PutReactionRequest(string? Kind);

/// <summary>
/// Reactions on published posts and on comments (E10-01, E10-02, FR-REACT, ADR-0061, ADR-0101): one reaction per person
/// and target, put, changed to another kind of the set in one transaction, and taken back; a repeat changes nothing. No
/// one reacts to their own content, and a guest comment takes no reactions. Putting needs a confirmed email
/// (ADR-0020); reading the counts does not need an account.
/// </summary>
public sealed class ReactionEndpoints : IApiEndpoints
{
    public const string PostNotFoundCode = "reactions.post_not_found";
    public const string CommentNotFoundCode = "reactions.comment_not_found";
    public const string OwnContentCode = "reactions.own_content";
    public const string GuestCommentCode = "reactions.guest_comment";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var posts = v1.MapGroup("/posts/{postId:guid}").WithTags("Reactions");
        posts.MapGet("/reactions", GetPostReactionsAsync)
            .Produces<PostReactionsView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetPostReactions")
            .WithSummary("The reactions of a published post and of its comments, and which of them are the account's");
        posts.MapPut("/reaction", PutPostReactionAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<ReactionsView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("PutPostReaction")
            .WithSummary("Puts a reaction of the set on a published post of another author, or changes the account's one to it");
        posts.MapDelete("/reaction", TakePostReactionAsync)
            .RequireAuthorization()
            .Produces<ReactionsView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("TakePostReaction")
            .WithSummary("Takes back the account's reaction on a post; taking back none changes nothing");

        var comments = v1.MapGroup("/comments/{id:guid}").WithTags("Reactions");
        comments.MapPut("/reaction", PutCommentReactionAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<ReactionsView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("PutCommentReaction")
            .WithSummary("Puts a reaction of the set on a comment of another account, or changes the account's one to it");
        comments.MapDelete("/reaction", TakeCommentReactionAsync)
            .RequireAuthorization()
            .Produces<ReactionsView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("TakeCommentReaction")
            .WithSummary("Takes back the account's reaction on a comment; taking back none changes nothing");
    }

    private static async Task<IResult> GetPostReactionsAsync(
        Guid postId, ClaimsPrincipal user, ReactionsDbContext db, IPostDirectory posts, CancellationToken ct)
    {
        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        var account = AccountOf(user);
        var counts = await db.Reactions.AsNoTracking()
            .Where(r => r.PostId == postId)
            .GroupBy(r => new { r.TargetKind, r.TargetId, r.Kind })
            .Select(g => new
            {
                g.Key.TargetKind,
                g.Key.TargetId,
                g.Key.Kind,
                Count = g.Count(),
                Mine = account != null && g.Any(r => r.AccountId == account),
            })
            .ToListAsync(ct);
        var targets = counts
            .GroupBy(c => (c.TargetKind, c.TargetId))
            .ToDictionary(
                g => g.Key,
                g => ViewOf(g.Select(c => (c.Kind, c.Count)), g.Where(c => c.Mine).Select(c => (ReactionKind?)c.Kind).FirstOrDefault()));
        var post = targets.GetValueOrDefault((ReactionTarget.Post, postId)) ?? new ReactionsView([], null);
        var commentReactions = targets
            .Where(t => t.Key.TargetKind == ReactionTarget.Comment)
            .OrderBy(t => t.Key.TargetId)
            .Select(t => new CommentReactionsView(t.Key.TargetId, t.Value.Reactions, t.Value.Mine))
            .ToList();
        return TypedResults.Ok(new PostReactionsView(postId, post, commentReactions));
    }

    private static async Task<IResult> PutPostReactionAsync(
        Guid postId,
        PutReactionRequest? request,
        ClaimsPrincipal user,
        ReactionsDbContext db,
        IModuleOutbox<ReactionsDbContext> outbox,
        IPostDirectory posts,
        TimeProvider time,
        IRealtimePublisher realtime,
        ILogger<ReactionEndpoints> logger,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await OfferedAsync(db, request?.Kind, ct) is not { } kind)
        {
            return UnknownKind();
        }

        if (await posts.FindPublishedAsync(postId, ct) is not { } post)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        return Refused(LikeRules.Refusal(account, post.AuthorAccountId))
            ?? await PutAsync(
                db,
                outbox,
                Reaction.Put(ReactionTarget.Post, postId, postId, account, kind, time.GetUtcNow()),
                post.AuthorAccountId,
                realtime,
                logger,
                ct);
    }

    private static async Task<IResult> TakePostReactionAsync(
        Guid postId,
        ClaimsPrincipal user,
        ReactionsDbContext db,
        IPostDirectory posts,
        IRealtimePublisher realtime,
        ILogger<ReactionEndpoints> logger,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        return await posts.FindPublishedAsync(postId, ct) is null
            ? ApiProblems.NotFound(PostNotFoundCode)
            : await TakeAsync(db, ReactionTarget.Post, postId, postId, account, realtime, logger, ct);
    }

    private static async Task<IResult> PutCommentReactionAsync(
        Guid id,
        PutReactionRequest? request,
        ClaimsPrincipal user,
        ReactionsDbContext db,
        IModuleOutbox<ReactionsDbContext> outbox,
        ICommentDirectory comments,
        TimeProvider time,
        IRealtimePublisher realtime,
        ILogger<ReactionEndpoints> logger,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await OfferedAsync(db, request?.Kind, ct) is not { } kind)
        {
            return UnknownKind();
        }

        if (await comments.FindShownAsync(id, ct) is not { } comment)
        {
            return ApiProblems.NotFound(CommentNotFoundCode);
        }

        return Refused(LikeRules.Refusal(account, comment.AuthorAccountId))
            ?? await PutAsync(
                db,
                outbox,
                Reaction.Put(ReactionTarget.Comment, id, comment.PostId, account, kind, time.GetUtcNow()),
                comment.AuthorAccountId!.Value,
                realtime,
                logger,
                ct);
    }

    private static async Task<IResult> TakeCommentReactionAsync(
        Guid id,
        ClaimsPrincipal user,
        ReactionsDbContext db,
        ICommentDirectory comments,
        IRealtimePublisher realtime,
        ILogger<ReactionEndpoints> logger,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        return await comments.FindShownAsync(id, ct) is not { } comment
            ? ApiProblems.NotFound(CommentNotFoundCode)
            : await TakeAsync(db, ReactionTarget.Comment, id, comment.PostId, account, realtime, logger, ct);
    }

    // A kind the platform knows, has a picture for and the directory offers now (ADR-0095, ADR-0101); a reaction no
    // longer offered stays where it was put, but is not put again.
    private static async Task<ReactionKind?> OfferedAsync(ReactionsDbContext db, string? code, CancellationToken ct) =>
        ReactionKinds.Parse(code) is { } kind
        && ReactionGraphics.Has(code)
        && await db.ReactionSet.AsNoTracking().AnyAsync(e => e.Code == code && e.Enabled, ct)
            ? kind
            : null;

    // The key is the person and the target (ADR-0061): a reaction already there is changed to the new kind in the
    // same transaction, under the lock of the target, so the counts never see both or neither (ADR-0101). The same
    // kind again, even one written by a request at the same moment, is the same reaction. Only a change reaches the
    // readers of the post (ADR-0072), and only a new reaction is announced (ADR-0092).
    private static async Task<IResult> PutAsync(
        ReactionsDbContext db,
        IModuleOutbox<ReactionsDbContext> outbox,
        Reaction reaction,
        Guid targetAuthor,
        IRealtimePublisher realtime,
        ILogger logger,
        CancellationToken ct)
    {
        var changed = false;
        try
        {
            // The reaction and the new rating of its target are one change (ADR-0081).
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await Ratings.LockAsync(db, reaction.TargetKind, reaction.TargetId, ct);
            var there = await db.Reactions.SingleOrDefaultAsync(
                r => r.TargetKind == reaction.TargetKind
                    && r.TargetId == reaction.TargetId
                    && r.AccountId == reaction.AccountId,
                ct);
            if (there is null)
            {
                db.Reactions.Add(reaction);
                await outbox.Publish(
                    new ReactionPut(
                        ReactionEvents.NameOf(reaction.TargetKind),
                        reaction.TargetId,
                        reaction.PostId,
                        targetAuthor,
                        reaction.AccountId,
                        ReactionEvents.NameOf(reaction.Kind),
                        reaction.CreatedAt),
                    ct);
                changed = true;
            }
            else if (there.Kind != reaction.Kind)
            {
                there.Kind = reaction.Kind;
                changed = true;
            }

            if (changed)
            {
                await db.SaveChangesAsync(ct);
                await Ratings.RecountAsync(db, reaction.TargetKind, reaction.TargetId, reaction.PostId, ct);
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        })
        {
            db.ChangeTracker.Clear();
            changed = false;
        }

        var state = await StateAsync(db, reaction.TargetKind, reaction.TargetId, reaction.AccountId, ct);
        if (changed)
        {
            await ReactionEvents.PublishAsync(
                realtime, logger, reaction.PostId, reaction.TargetKind, reaction.TargetId, state.Reactions, ct);
        }

        return TypedResults.Ok(state);
    }

    private static async Task<IResult> TakeAsync(
        ReactionsDbContext db,
        ReactionTarget target,
        Guid targetId,
        Guid postId,
        Guid account,
        IRealtimePublisher realtime,
        ILogger logger,
        CancellationToken ct)
    {
        int removed;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await Ratings.LockAsync(db, target, targetId, ct);
            removed = await db.Reactions
                .Where(r => r.TargetKind == target && r.TargetId == targetId && r.AccountId == account)
                .ExecuteDeleteAsync(ct);
            if (removed > 0)
            {
                await Ratings.RecountAsync(db, target, targetId, postId, ct);
            }

            await transaction.CommitAsync(ct);
        }

        var state = await StateAsync(db, target, targetId, account, ct);
        if (removed > 0)
        {
            await ReactionEvents.PublishAsync(realtime, logger, postId, target, targetId, state.Reactions, ct);
        }

        return TypedResults.Ok(state);
    }

    private static async Task<ReactionsView> StateAsync(
        ReactionsDbContext db, ReactionTarget target, Guid targetId, Guid account, CancellationToken ct)
    {
        var reactions = db.Reactions.AsNoTracking().Where(r => r.TargetKind == target && r.TargetId == targetId);
        var counts = await reactions
            .GroupBy(r => r.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var mine = await reactions
            .Where(r => r.AccountId == account)
            .Select(r => (ReactionKind?)r.Kind)
            .SingleOrDefaultAsync(ct);
        return ViewOf(counts.Select(c => (c.Kind, c.Count)), mine);
    }

    private static ReactionsView ViewOf(IEnumerable<(ReactionKind Kind, int Count)> counts, ReactionKind? mine) =>
        new(
            [.. counts
                .Where(c => c.Count > 0)
                .OrderBy(c => c.Kind)
                .Select(c => new ReactionCountView(ReactionKinds.CodeOf(c.Kind), c.Count))],
            mine is { } kind ? ReactionKinds.CodeOf(kind) : null);

    private static IResult UnknownKind() => ApiProblems.Validation(
        new Dictionary<string, string[]> { ["kind"] = ["Такой реакции нет в наборе."] });

    private static IResult? Refused(LikeRefusal? refusal) => refusal switch
    {
        LikeRefusal.OwnContent => ApiProblems.Forbidden(OwnContentCode, "На свой пост или комментарий реакцию не ставят."),
        LikeRefusal.GuestComment => ApiProblems.Forbidden(
            GuestCommentCode, "На комментарий гостя реакцию не поставить: у его автора нет аккаунта."),
        _ => null,
    };

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
