using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Preferences.Contracts;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Comments;

/// <summary>A new comment.</summary>
/// <param name="Text">The text; trimmed, from 1 to 5000 characters.</param>
/// <param name="ParentId">The comment of the same post it replies to; missing for a reply to the post.</param>
public sealed record CommentInput(string? Text, Guid? ParentId = null);

/// <summary>The new text of a comment (E09-05).</summary>
/// <param name="Text">The text; trimmed, from 1 to 5000 characters.</param>
public sealed record CommentEditInput(string? Text);

/// <summary>The comments of the signed-in account under a post, for the actions only its author has (E09-05).</summary>
/// <param name="PostId">The post.</param>
/// <param name="Ids">The account's comments under it that are not deleted.</param>
public sealed record OwnCommentsView(Guid PostId, IReadOnlyList<Guid> Ids);

/// <summary>The outside author of a guest comment.</summary>
/// <param name="Name">The name shown instead of a blog.</param>
/// <param name="Url">The profile or the message in the outside service; <c>null</c> when unknown.</param>
public sealed record GuestAuthorView(string Name, string? Url);

/// <summary>A comment (FR-COMMENT).</summary>
/// <param name="Id">The comment's identifier.</param>
/// <param name="PostId">The post it is under.</param>
/// <param name="ParentId">The comment it replies to; <c>null</c> for a reply to the post.</param>
/// <param name="Depth">Its level in the tree: 1 for a reply to the post.</param>
/// <param name="Kind"><c>native</c> (an account of the platform) or <c>guest</c> (an outside author, FR-TG-6).</param>
/// <param name="AuthorAccountId">The author of a native comment; <c>null</c> for a guest one and a deleted one.</param>
/// <param name="Guest">The author of a guest comment; <c>null</c> for a native one and a deleted one.</param>
/// <param name="Text">The text; empty for a deleted comment.</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="EditedAt">When its author last changed the text («изменено»); <c>null</c> for a text never changed.</param>
/// <param name="EditableUntil">
/// Until when its author may change the text (ADR-0059); <c>null</c> for a guest comment and a deleted one.
/// </param>
/// <param name="Deleted">
/// Deleted by its author: the stub «Комментарий удалён» that holds the place of its replies, with no author and no text.
/// </param>
/// <param name="Replies">
/// Every reply under it at any depth, those the read left out too (ADR-0054): when it is more than the replies shown,
/// the rest come with its branch.
/// </param>
/// <param name="AuthorBlocked">
/// The signed-in reader blocked its author (E11-06, ADR-0091): the page shows it folded, with «Показать». Always
/// <c>false</c> for a guest reader.
/// </param>
public sealed record CommentView(
    Guid Id,
    Guid PostId,
    Guid? ParentId,
    int Depth,
    string Kind,
    Guid? AuthorAccountId,
    GuestAuthorView? Guest,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    DateTimeOffset? EditableUntil,
    bool Deleted,
    int Replies,
    bool AuthorBlocked);

/// <summary>A tree or a branch of comments, in the order of the tree.</summary>
/// <param name="PostId">The post the comments are under.</param>
/// <param name="MaxDepth">The most levels of a branch: a comment at this depth takes no replies.</param>
/// <param name="Comments">
/// Depth first: a parent before its replies, older replies first. The comments under the post come in the order the
/// read asked for (<c>sort</c>); a branch is one thread and keeps the order of the tree.
/// </param>
public sealed record CommentTreeView(Guid PostId, int MaxDepth, IReadOnlyList<CommentView> Comments);

/// <summary>
/// The comments of a published post (FR-COMMENT, ADR-0048): read the whole tree or one branch, each with one query,
/// and reply to the post or to a comment; its author changes the text for a while and deletes it (E09-05, ADR-0059).
/// Writing needs a confirmed email (ADR-0020).
/// </summary>
public sealed class CommentEndpoints : IApiEndpoints
{
    public const string PostNotFoundCode = "comments.post_not_found";
    public const string CommentNotFoundCode = "comments.comment_not_found";
    public const string NotAuthorCode = "comments.not_author";
    public const string EditClosedCode = "comments.edit_closed";
    public const string BlockedCode = "comments.blocked";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var underPost = v1.MapGroup("/posts/{postId:guid}/comments").WithTags("Comments");
        underPost.MapGet("/", GetTreeAsync)
            .Produces<CommentTreeView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetCommentTree")
            .ProducesValidationProblem()
            .WithSummary("The comments under a published post, popular or newest threads first, all or some levels down");
        underPost.MapPost("/", CreateAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<CommentView>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("CreateComment")
            .WithSummary("Replies to a published post or to one of its comments");
        underPost.MapGet("/mine", GetOwnAsync)
            .RequireAuthorization()
            .Produces<OwnCommentsView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetOwnComments")
            .WithSummary("The signed-in account's comments under a published post");

        var comments = v1.MapGroup("/comments").WithTags("Comments");
        comments.MapGet("/{id:guid}/branch", GetBranchAsync)
            .Produces<CommentTreeView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetCommentBranch")
            .ProducesValidationProblem()
            .WithSummary("A comment and the replies under it in the order of the tree, all or some levels down");
        comments.MapPatch("/{id:guid}", EditAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<CommentView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("EditComment")
            .WithSummary("Changes the text of the author's own comment while the time to change it lasts");
        comments.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DeleteComment")
            .WithSummary("Deletes the author's own comment: one with replies stays as the stub of its replies");
    }

    private static async Task<IResult> GetTreeAsync(
        Guid postId,
        [FromQuery] int? levels,
        [FromQuery] string? sort,
        CommentsDbContext db,
        IPostDirectory posts,
        IReactionRatings ratings,
        CommentLimits limits,
        ClaimsPrincipal user,
        IBlockList blocks,
        CancellationToken ct)
    {
        if (LevelsProblem(levels) is { } problem)
        {
            return problem;
        }

        if (SortOf(sort) is not { } order)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["sort"] = ["Порядок комментариев — popular или date."],
            });
        }

        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        var tree = await CommentTree.OfPost(db, postId).ToListAsync(ct);
        var rated = order == CommentSort.Popular ? await ratings.CommentRatingsAsync([postId], ct) : null;
        var shown = CommentTree.Sorted(CommentTree.Shown(tree, 0, levels), order, rated);
        var blocked = await BlockedByReaderAsync(user, blocks, ct);
        return TypedResults.Ok(new CommentTreeView(postId, limits.MaxDepth, Views(shown, limits, blocked)));
    }

    private static async Task<IResult> GetBranchAsync(
        Guid id,
        [FromQuery] int? levels,
        CommentsDbContext db,
        IPostDirectory posts,
        CommentLimits limits,
        ClaimsPrincipal user,
        IBlockList blocks,
        CancellationToken ct)
    {
        if (LevelsProblem(levels) is { } problem)
        {
            return problem;
        }

        var branch = await CommentTree.Branch(db, id).ToListAsync(ct);
        if (branch.Count == 0 || await posts.FindPublishedAsync(branch[0].PostId, ct) is null)
        {
            return ApiProblems.NotFound(CommentNotFoundCode);
        }

        var shown = CommentTree.Shown(branch, branch[0].Depth, levels);
        var blocked = await BlockedByReaderAsync(user, blocks, ct);
        return TypedResults.Ok(new CommentTreeView(branch[0].PostId, limits.MaxDepth, Views(shown, limits, blocked)));
    }

    private static async Task<IResult> GetOwnAsync(
        Guid postId,
        ClaimsPrincipal user,
        CommentsDbContext db,
        IPostDirectory posts,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        var ids = await db.Comments.AsNoTracking()
            .Where(c => c.PostId == postId && c.AuthorAccountId == account && c.DeletedAt == null)
            .OrderBy(c => c.Path)
            .Select(c => c.Id)
            .ToListAsync(ct);
        return TypedResults.Ok(new OwnCommentsView(postId, ids));
    }

    private static async Task<IResult> EditAsync(
        Guid id,
        CommentEditInput input,
        ClaimsPrincipal user,
        CommentsDbContext db,
        IPostDirectory posts,
        CommentLimits limits,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var comment = await db.Comments.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (comment is null || await posts.FindPublishedAsync(comment.PostId, ct) is null)
        {
            return ApiProblems.NotFound(CommentNotFoundCode);
        }

        var now = time.GetUtcNow();
        if (Refusal(CommentRules.ChangeProblem(comment, account, edit: true, now, limits.EditWindow)) is { } refused)
        {
            return refused;
        }

        var text = CommentRules.CleanText(input.Text);
        if (TextProblem(text) is { } problem)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["text"] = [problem],
            });
        }

        comment.Edit(text!, now);
        await db.SaveChangesAsync(ct);
        var under = comment.Path + CommentPath.Separator;
        var replies = await db.Comments.CountAsync(c => c.PostId == comment.PostId && c.Path.StartsWith(under), ct);
        return TypedResults.Ok(View(comment, replies, limits));
    }

    // A comment with replies stays as their stub; one without them goes, and so does every stub above it that is left
    // with no replies (ADR-0059): a stub holds the place of replies only while there are some.
    private static async Task<IResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal user,
        CommentsDbContext db,
        IPostDirectory posts,
        CommentLimits limits,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var comment = await db.Comments.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (comment is null || await posts.FindPublishedAsync(comment.PostId, ct) is null)
        {
            return ApiProblems.NotFound(CommentNotFoundCode);
        }

        var now = time.GetUtcNow();
        if (Refusal(CommentRules.ChangeProblem(comment, account, edit: false, now, limits.EditWindow)) is { } refused)
        {
            return refused;
        }

        // Its thread holds every comment the deletion may touch: the stubs above it and the replies under it.
        var root = comment.Path.Split(CommentPath.Separator)[0];
        var thread = await db.Comments
            .Where(c => c.PostId == comment.PostId && c.Path.StartsWith(root))
            .OrderBy(c => c.Path)
            .ToListAsync(ct);
        db.Comments.RemoveRange(CommentTree.Delete(thread, comment.Id, now));
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> CreateAsync(
        Guid postId,
        CommentInput input,
        ClaimsPrincipal user,
        CommentsDbContext db,
        IPostDirectory posts,
        CommentLimits limits,
        TimeProvider time,
        IBlockList blocks,
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

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var text = CommentRules.CleanText(input.Text);
        if (TextProblem(text) is { } textProblem)
        {
            errors["text"] = [textProblem];
        }

        Comment? parent = null;
        if (input.ParentId is { } parentId)
        {
            parent = await db.Comments.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == parentId && c.PostId == postId, ct);
            if ((parent is null ? "Под этим постом нет такого комментария." : CommentRules.ReplyProblem(parent, limits.MaxDepth))
                is { } refused)
            {
                errors["parentId"] = [refused];
            }
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        // A blocked account writes neither under the post of the one who blocked it nor in reply to their comment
        // (E11-06, ADR-0091).
        if (await blocks.IsBlockedAsync(post.AuthorAccountId, account, ct)
            || (parent?.AuthorAccountId is { } addressee && !parent.IsDeleted
                && await blocks.IsBlockedAsync(addressee, account, ct)))
        {
            return ApiProblems.Forbidden(BlockedCode, "Автор заблокировал вас: отвечать ему нельзя.");
        }

        var comment = Comment.CreateNative(account, postId, parent, text!, time.GetUtcNow());
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/comments/{comment.Id}/branch", View(comment, replies: 0, limits));
    }

    private static string? TextProblem(string? text) =>
        text is null ? "Напишите текст комментария."
        : text.Length > CommentRules.MaxTextLength ? $"Комментарий не длиннее {CommentRules.MaxTextLength} символов."
        : null;

    private static IResult? Refusal(CommentChange change) => change switch
    {
        CommentChange.NotFound => ApiProblems.NotFound(CommentNotFoundCode),
        CommentChange.NotAuthor => ApiProblems.Forbidden(NotAuthorCode, "Менять и удалять комментарий может только автор."),
        CommentChange.EditClosed => ApiProblems.Forbidden(
            EditClosedCode, "Время на правку вышло: комментарий можно только удалить."),
        _ => null,
    };

    // A read may stop some levels down (ADR-0054); any other number is refused rather than read as the whole tree.
    private static IResult? LevelsProblem(int? levels) =>
        levels is null or (>= 1 and <= CommentsSettings.MaxDepthLimit)
            ? null
            : ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["levels"] = [$"Число уровней — от 1 до {CommentsSettings.MaxDepthLimit}."],
            });

    // «Популярные» unless the read asks for «По дате» (ADR-0058); any other word is refused, not read as the default.
    private static CommentSort? SortOf(string? sort) => sort switch
    {
        null or "popular" => CommentSort.Popular,
        "date" => CommentSort.Date,
        _ => null,
    };

    private static CommentView[] Views(
        IReadOnlyList<ShownComment> shown, CommentLimits limits, IReadOnlySet<Guid> blocked) =>
        [.. shown.Select(s => View(s.Comment, s.Replies, limits, blocked))];

    // The accounts the signed-in reader blocked (ADR-0091); none for a guest.
    private static async Task<IReadOnlySet<Guid>> BlockedByReaderAsync(
        ClaimsPrincipal user, IBlockList blocks, CancellationToken ct) =>
        AccountOf(user) is { } reader ? await blocks.BlockedByAsync(reader, ct) : new HashSet<Guid>();

    // A stub names no author: the comment is gone, only the place of its replies stays (ADR-0059).
    internal static CommentView View(
        Comment comment, int replies, CommentLimits limits, IReadOnlySet<Guid>? blocked = null) => new(
        comment.Id,
        comment.PostId,
        comment.ParentId,
        comment.Depth,
        comment.Kind == CommentKind.Guest ? "guest" : "native",
        comment.IsDeleted ? null : comment.AuthorAccountId,
        !comment.IsDeleted && comment.Guest is { } guest ? new GuestAuthorView(guest.Name, guest.Url?.AbsoluteUri) : null,
        comment.Text,
        comment.CreatedAt,
        comment.IsDeleted ? null : comment.EditedAt,
        CommentRules.EditableUntil(comment, limits.EditWindow),
        comment.IsDeleted,
        replies,
        !comment.IsDeleted && comment.AuthorAccountId is { } author && blocked?.Contains(author) == true);

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
