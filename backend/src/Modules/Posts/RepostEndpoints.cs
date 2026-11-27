using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;
using Platform.Messaging;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Posts;

/// <summary>What a repost is made of (E10-08, ADR-0117).</summary>
/// <param name="Text">What the reposter writes over it; empty or missing for a repost without a text.</param>
public sealed record RepostInput(string? Text = null);

/// <summary>A repost in the list of a post's reposts (E10-08, ADR-0117).</summary>
/// <param name="Id">The repost: a post in the reposter's personal blog.</param>
/// <param name="AuthorAccountId">Who reposted.</param>
/// <param name="Author">The reposter's personal blog with the name and the avatar; <c>null</c> when there is none.</param>
/// <param name="Text">What the reposter wrote over it; <c>null</c> without a text.</param>
/// <param name="PublishedAt">When it was reposted.</param>
public sealed record RepostView(
    Guid Id, Guid AuthorAccountId, FeedBlogView? Author, string? Text, DateTimeOffset PublishedAt);

/// <summary>
/// <c>/api/v1/posts/{id}/reposts</c> (FR-REACT, E10-08, ADR-0117): repost someone else's published post into one's
/// personal blog, with a text or without, and the list of a post's reposts, newest first. A repost is a post of its
/// own (<see cref="Post.RepostOfPostId"/>), so it is in its author's blog and in the «Моя лента» of their subscribers;
/// deleting the original hides its reposts everywhere. A new repost tells the original's author through
/// <see cref="PostReposted"/>.
/// </summary>
public sealed class RepostEndpoints : IApiEndpoints
{
    public const string OwnPostCode = "posts.repost_own_post";
    public const string AlreadyRepostedCode = "posts.already_reposted";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var posts = v1.MapGroup("/posts").WithTags("Posts");
        posts.MapPost("/{id:guid}/reposts", CreateAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<PostView>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("RepostPost")
            .WithSummary("Reposts someone else's published post into the signed-in person's blog, with a text or without");
        posts.MapGet("/{id:guid}/reposts", ListAsync)
            .Produces<CursorPage<RepostView>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetPostReposts")
            .WithSummary("The reposts of a published post, newest first, page by page with a cursor");
    }

    private static async Task<IResult> CreateAsync(
        Guid id,
        RepostInput? input,
        ClaimsPrincipal user,
        PostsDbContext db,
        IModuleOutbox<PostsDbContext> outbox,
        IBlogDirectory blogs,
        IMediaLibrary media,
        FeedPageCache feed,
        TimeProvider time,
        CancellationToken ct)
    {
        if (PostEndpoints.AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var original = await db.Posts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (original?.RepostOfPostId is { } of)
        {
            // Reposting a repost reposts what it carries.
            original = await db.Posts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == of, ct);
        }

        var text = input?.Text;
        switch (original is null ? RepostRefusal.NotFound : PostRules.RepostRefusalOf(original, account, text))
        {
            case RepostRefusal.NotFound:
                return ApiProblems.NotFound(PostEndpoints.PostNotFoundCode);
            case RepostRefusal.OwnPost:
                return ApiProblems.Forbidden(OwnPostCode, "One's own post is not reposted.");
            case RepostRefusal.TextTooLong:
                return ApiProblems.Validation(new Dictionary<string, string[]>
                {
                    ["text"] = [$"Не длиннее {PostRules.RepostTextMaxLength} символов."],
                });
        }

        if (await db.Posts.AnyAsync(p => p.AuthorAccountId == account && p.RepostOfPostId == original!.Id, ct))
        {
            return AlreadyReposted();
        }

        var blog = await blogs.EnsurePersonalBlogAsync(account, ct);
        var now = time.GetUtcNow();
        var repost = Post.CreateRepost(account, blog.Id, original!.Id, text, now);
        db.Posts.Add(repost);
        // The repost and its event are one change (ADR-0092): the author is told only of a repost that is there.
        await outbox.Publish(
            new PostReposted(repost.Id, original.Id, original.AuthorAccountId, account, now), ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: PostsDbContext.RepostIndex,
        })
        {
            // Two reposts at once: the other one won.
            return AlreadyReposted();
        }

        await feed.InvalidateAsync(ct);
        return TypedResults.Created($"/api/v1/posts/{repost.Id}", await PostEndpoints.ViewAsync(repost, media, ct));
    }

    private static async Task<IResult> ListAsync(
        Guid id,
        [AsParameters] PageQuery query,
        ClaimsPrincipal user,
        PostsDbContext db,
        IBlogDirectory blogs,
        IEnumerable<IFeedExclusionSource> exclusionSources,
        CancellationToken ct)
    {
        var page = query.ToRequest<FreshFeedCursor>();
        if (!await db.Posts.AnyAsync(
                p => p.Id == id && p.Status == PostStatus.Published && p.RepostOfPostId == null, ct))
        {
            return ApiProblems.NotFound(PostEndpoints.PostNotFoundCode);
        }

        // The reader's black list keeps its people out of the list, as out of the feeds (ADR-0091).
        var blocked = (await ReaderExclusions.ForAsync(user, exclusionSources, ct)).Authors.ToList();
        var reposts = db.Posts.AsNoTracking()
            .Where(p => p.RepostOfPostId == id && p.Status == PostStatus.Published && p.PublishedAt != null)
            .Where(p => !blocked.Contains(p.AuthorAccountId));
        if (page.After is { } after)
        {
            reposts = reposts.Where(p => EF.Functions.LessThan(
                ValueTuple.Create(p.PublishedAt!.Value, p.Id), ValueTuple.Create(after.PublishedAt, after.Id)));
        }

        var rows = await reposts
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
            .Take(page.FetchCount)
            .ToListAsync(ct);
        var result = page.ToPage(rows, p => new FreshFeedCursor(p.PublishedAt!.Value, p.Id));
        var cards = await blogs.CardsAsync([], [.. result.Items.Select(r => r.AuthorAccountId).Distinct()], ct);
        var byOwner = cards
            .Where(c => c.Kind == BlogKind.Personal && c.OwnerAccountId is not null)
            .GroupBy(c => c.OwnerAccountId!.Value)
            .ToDictionary(g => g.Key, g => FeedPostViews.View(g.First()));
        return TypedResults.Ok(new CursorPage<RepostView>(
            [
                .. result.Items.Select(r => new RepostView(
                    r.Id,
                    r.AuthorAccountId,
                    byOwner.GetValueOrDefault(r.AuthorAccountId),
                    r.RepostText,
                    r.PublishedAt!.Value)),
            ],
            result.NextCursor));
    }

    private static IResult AlreadyReposted() =>
        ApiProblems.Conflict(AlreadyRepostedCode, "The post is already reposted by this person.");
}
