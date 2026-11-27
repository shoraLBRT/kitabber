using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Reactions;

/// <summary>The bookmark state of a post or a comment for the signed-in account (FR-REACT, ADR-0113).</summary>
/// <param name="Bookmarked">Whether the account keeps it in its bookmarks.</param>
/// <param name="Count">How many people keep it: only for its author, <c>null</c> for everyone else.</param>
public sealed record BookmarkView(bool Bookmarked, int? Count);

/// <summary>The bookmark state of one post or comment.</summary>
/// <param name="Id">The post or the comment.</param>
/// <param name="Bookmarked">Whether the account keeps it in its bookmarks.</param>
/// <param name="Count">How many people keep it: only for its author, <c>null</c> for everyone else.</param>
public sealed record BookmarkMarkView(Guid Id, bool Bookmarked, int? Count);

/// <summary>The bookmark state of a post and of its comments, for the post page.</summary>
/// <param name="PostId">The post.</param>
/// <param name="Post">The state of the post itself.</param>
/// <param name="Comments">The comments under it the account keeps or wrote and someone keeps; a comment not listed has neither.</param>
public sealed record PostBookmarksView(Guid PostId, BookmarkView Post, IReadOnlyList<BookmarkMarkView> Comments);

/// <summary>The bookmark state of the posts of a feed.</summary>
/// <param name="Posts">The asked posts the account keeps or wrote and someone keeps; a post not listed has neither.</param>
public sealed record FeedBookmarksView(IReadOnlyList<BookmarkMarkView> Posts);

/// <summary>
/// Bookmarks on published posts and on shown comments (E10-07, FR-REACT, ADR-0113): one per person and target, put and
/// taken back; a repeat changes nothing. Bookmarks are private: every route needs an account, the answer tells the
/// account only about its own bookmarks, and the count of people who keep a post or a comment is shown only to its
/// author. Anyone signed in may bookmark anything they can read, their own content and guest comments included.
/// </summary>
public sealed class BookmarkEndpoints : IApiEndpoints
{
    public const string PostNotFoundCode = "bookmarks.post_not_found";
    public const string CommentNotFoundCode = "bookmarks.comment_not_found";

    /// <summary>The most posts one feed request asks about: more than two pages of a feed.</summary>
    public const int MaxFeedPosts = 100;

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var posts = v1.MapGroup("/posts/{postId:guid}").WithTags("Bookmarks");
        posts.MapGet("/bookmarks", GetPostBookmarksAsync)
            .RequireAuthorization()
            .Produces<PostBookmarksView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetPostBookmarks")
            .WithSummary("Which of a published post and its comments the account keeps, and the author's counts");
        posts.MapPut("/bookmark", PutPostBookmarkAsync)
            .RequireAuthorization()
            .Produces<BookmarkView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("PutPostBookmark")
            .WithSummary("Keeps a published post in the account's bookmarks; keeping it again changes nothing");
        posts.MapDelete("/bookmark", TakePostBookmarkAsync)
            .RequireAuthorization()
            .Produces<BookmarkView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("TakePostBookmark")
            .WithSummary("Takes a post out of the account's bookmarks; taking out none changes nothing");

        var comments = v1.MapGroup("/comments/{id:guid}").WithTags("Bookmarks");
        comments.MapPut("/bookmark", PutCommentBookmarkAsync)
            .RequireAuthorization()
            .Produces<BookmarkView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("PutCommentBookmark")
            .WithSummary("Keeps a shown comment in the account's bookmarks; keeping it again changes nothing");
        comments.MapDelete("/bookmark", TakeCommentBookmarkAsync)
            .RequireAuthorization()
            .Produces<BookmarkView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("TakeCommentBookmark")
            .WithSummary("Takes a comment out of the account's bookmarks; taking out none changes nothing");

        v1.MapGet("/bookmarks/posts", GetFeedBookmarksAsync)
            .WithTags("Bookmarks")
            .RequireAuthorization()
            .Produces<FeedBookmarksView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetFeedBookmarks")
            .WithSummary("Which of the given posts the account keeps, and the author's counts, for the cards of a feed");
    }

    private static async Task<IResult> GetPostBookmarksAsync(
        Guid postId, ClaimsPrincipal user, ReactionsDbContext db, IPostDirectory posts, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        var marks = await MarksAsync(db.Bookmarks.Where(b => b.PostId == postId), account, ct);
        var post = marks.Where(m => m.Kind == ReactionTarget.Post).Select(m => m.Mark).FirstOrDefault() is { } own
            ? new BookmarkView(own.Bookmarked, own.Count)
            : new BookmarkView(false, null);
        var commentMarks = marks
            .Where(m => m.Kind == ReactionTarget.Comment)
            .Select(m => m.Mark)
            .OrderBy(m => m.Id)
            .ToList();
        return TypedResults.Ok(new PostBookmarksView(postId, post, commentMarks));
    }

    private static async Task<IResult> GetFeedBookmarksAsync(
        [FromQuery(Name = "ids")] string[]? ids, ClaimsPrincipal user, ReactionsDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var asked = new HashSet<Guid>();
        foreach (var id in ids ?? [])
        {
            if (!Guid.TryParse(id, out var postId))
            {
                return ApiProblems.Validation(
                    new Dictionary<string, string[]> { ["ids"] = ["Это не идентификатор поста."] });
            }

            asked.Add(postId);
        }

        if (asked.Count > MaxFeedPosts)
        {
            return ApiProblems.Validation(
                new Dictionary<string, string[]> { ["ids"] = [$"Не больше {MaxFeedPosts} постов за запрос."] });
        }

        if (asked.Count == 0)
        {
            return TypedResults.Ok(new FeedBookmarksView([]));
        }

        var marks = await MarksAsync(
            db.Bookmarks.Where(b => b.TargetKind == ReactionTarget.Post && asked.Contains(b.TargetId)), account, ct);
        return TypedResults.Ok(new FeedBookmarksView([.. marks.Select(m => m.Mark).OrderBy(m => m.Id)]));
    }

    private static async Task<IResult> PutPostBookmarkAsync(
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

        return await posts.FindPublishedAsync(postId, ct) is not { } post
            ? ApiProblems.NotFound(PostNotFoundCode)
            : await PutAsync(
                db,
                Bookmark.Put(ReactionTarget.Post, postId, postId, account, post.AuthorAccountId, time.GetUtcNow()),
                ct);
    }

    private static async Task<IResult> TakePostBookmarkAsync(
        Guid postId, ClaimsPrincipal user, ReactionsDbContext db, IPostDirectory posts, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        return await posts.FindPublishedAsync(postId, ct) is null
            ? ApiProblems.NotFound(PostNotFoundCode)
            : await TakeAsync(db, ReactionTarget.Post, postId, account, ct);
    }

    private static async Task<IResult> PutCommentBookmarkAsync(
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

        return await comments.FindShownAsync(id, ct) is not { } comment
            ? ApiProblems.NotFound(CommentNotFoundCode)
            : await PutAsync(
                db,
                Bookmark.Put(
                    ReactionTarget.Comment, id, comment.PostId, account, comment.AuthorAccountId, time.GetUtcNow()),
                ct);
    }

    private static async Task<IResult> TakeCommentBookmarkAsync(
        Guid id, ClaimsPrincipal user, ReactionsDbContext db, ICommentDirectory comments, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        return await comments.FindShownAsync(id, ct) is null
            ? ApiProblems.NotFound(CommentNotFoundCode)
            : await TakeAsync(db, ReactionTarget.Comment, id, account, ct);
    }

    // The key is the person and the target: a bookmark already there, even one written by a request at the same
    // moment, is the same bookmark.
    private static async Task<IResult> PutAsync(ReactionsDbContext db, Bookmark bookmark, CancellationToken ct)
    {
        var there = await db.Bookmarks.AnyAsync(
            b => b.TargetKind == bookmark.TargetKind
                && b.TargetId == bookmark.TargetId
                && b.AccountId == bookmark.AccountId,
            ct);
        if (!there)
        {
            db.Bookmarks.Add(bookmark);
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

        return TypedResults.Ok(await StateAsync(db, bookmark.TargetKind, bookmark.TargetId, bookmark.AccountId, ct));
    }

    private static async Task<IResult> TakeAsync(
        ReactionsDbContext db, ReactionTarget target, Guid targetId, Guid account, CancellationToken ct)
    {
        await db.Bookmarks
            .Where(b => b.TargetKind == target && b.TargetId == targetId && b.AccountId == account)
            .ExecuteDeleteAsync(ct);
        return TypedResults.Ok(await StateAsync(db, target, targetId, account, ct));
    }

    private static async Task<BookmarkView> StateAsync(
        ReactionsDbContext db, ReactionTarget target, Guid targetId, Guid account, CancellationToken ct)
    {
        var marks = await MarksAsync(
            db.Bookmarks.Where(b => b.TargetKind == target && b.TargetId == targetId), account, ct);
        return marks.Count == 0
            ? new BookmarkView(false, null)
            : new BookmarkView(marks[0].Mark.Bookmarked, marks[0].Mark.Count);
    }

    // Per target of the bookmarks: whether the account keeps it and, for the target's author, how many people do. A
    // target the account neither keeps nor wrote is left out: its count is not the account's to know.
    private static async Task<List<(ReactionTarget Kind, BookmarkMarkView Mark)>> MarksAsync(
        IQueryable<Bookmark> bookmarks, Guid account, CancellationToken ct)
    {
        var groups = await bookmarks.AsNoTracking()
            .GroupBy(b => new { b.TargetKind, b.TargetId })
            .Select(g => new
            {
                g.Key.TargetKind,
                g.Key.TargetId,
                Count = g.Count(),
                Mine = g.Any(b => b.AccountId == account),
                Authored = g.Any(b => b.TargetAuthorId == account),
            })
            .Where(g => g.Mine || g.Authored)
            .ToListAsync(ct);
        return
        [
            .. groups.Select(g => (
                g.TargetKind,
                new BookmarkMarkView(g.TargetId, g.Mine, g.Authored ? g.Count : (int?)null))),
        ];
    }

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
