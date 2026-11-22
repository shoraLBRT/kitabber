using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Comments;

/// <summary>A new comment.</summary>
/// <param name="Text">The text; trimmed, from 1 to 5000 characters.</param>
/// <param name="ParentId">The comment of the same post it replies to; missing for a reply to the post.</param>
public sealed record CommentInput(string? Text, Guid? ParentId = null);

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
/// <param name="AuthorAccountId">The author of a native comment; <c>null</c> for a guest one.</param>
/// <param name="Guest">The author of a guest comment; <c>null</c> for a native one.</param>
/// <param name="Text">The text.</param>
/// <param name="CreatedAt">When it was written.</param>
public sealed record CommentView(
    Guid Id,
    Guid PostId,
    Guid? ParentId,
    int Depth,
    string Kind,
    Guid? AuthorAccountId,
    GuestAuthorView? Guest,
    string Text,
    DateTimeOffset CreatedAt);

/// <summary>A tree or a branch of comments, in the order of the tree.</summary>
/// <param name="PostId">The post the comments are under.</param>
/// <param name="MaxDepth">The most levels of a branch: a comment at this depth takes no replies.</param>
/// <param name="Comments">Depth first: a parent before its replies, older siblings first.</param>
public sealed record CommentTreeView(Guid PostId, int MaxDepth, IReadOnlyList<CommentView> Comments);

/// <summary>
/// The comments of a published post (FR-COMMENT, ADR-0048): read the whole tree or one branch, each with one query,
/// and reply to the post or to a comment. Writing needs a confirmed email (ADR-0020).
/// </summary>
public sealed class CommentEndpoints : IApiEndpoints
{
    public const string PostNotFoundCode = "comments.post_not_found";
    public const string CommentNotFoundCode = "comments.comment_not_found";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var underPost = v1.MapGroup("/posts/{postId:guid}/comments").WithTags("Comments");
        underPost.MapGet("/", GetTreeAsync)
            .Produces<CommentTreeView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetCommentTree")
            .WithSummary("Every comment under a published post, in the order of the tree");
        underPost.MapPost("/", CreateAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<CommentView>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("CreateComment")
            .WithSummary("Replies to a published post or to one of its comments");

        var comments = v1.MapGroup("/comments").WithTags("Comments");
        comments.MapGet("/{id:guid}/branch", GetBranchAsync)
            .Produces<CommentTreeView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetCommentBranch")
            .WithSummary("A comment and every reply under it, in the order of the tree");
    }

    private static async Task<IResult> GetTreeAsync(
        Guid postId, CommentsDbContext db, IPostDirectory posts, CommentLimits limits, CancellationToken ct)
    {
        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        var tree = await CommentTree.OfPost(db, postId).ToListAsync(ct);
        return TypedResults.Ok(new CommentTreeView(postId, limits.MaxDepth, [.. tree.Select(View)]));
    }

    private static async Task<IResult> GetBranchAsync(
        Guid id, CommentsDbContext db, IPostDirectory posts, CommentLimits limits, CancellationToken ct)
    {
        var branch = await CommentTree.Branch(db, id).ToListAsync(ct);
        if (branch.Count == 0 || await posts.FindPublishedAsync(branch[0].PostId, ct) is null)
        {
            return ApiProblems.NotFound(CommentNotFoundCode);
        }

        return TypedResults.Ok(new CommentTreeView(branch[0].PostId, limits.MaxDepth, [.. branch.Select(View)]));
    }

    private static async Task<IResult> CreateAsync(
        Guid postId,
        CommentInput input,
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

        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var text = CommentRules.CleanText(input.Text);
        if (text is null)
        {
            errors["text"] = ["Напишите текст комментария."];
        }
        else if (text.Length > CommentRules.MaxTextLength)
        {
            errors["text"] = [$"Комментарий не длиннее {CommentRules.MaxTextLength} символов."];
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

        var comment = Comment.CreateNative(account, postId, parent, text!, time.GetUtcNow());
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/comments/{comment.Id}/branch", View(comment));
    }

    internal static CommentView View(Comment comment) => new(
        comment.Id,
        comment.PostId,
        comment.ParentId,
        comment.Depth,
        comment.Kind == CommentKind.Guest ? "guest" : "native",
        comment.AuthorAccountId,
        comment.Guest is { } guest ? new GuestAuthorView(guest.Name, guest.Url?.AbsoluteUri) : null,
        comment.Text,
        comment.CreatedAt);

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
