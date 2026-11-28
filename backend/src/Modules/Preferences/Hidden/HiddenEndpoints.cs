using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Preferences.Hidden;

/// <summary>A post hidden from the feeds.</summary>
/// <param name="PostId">The hidden post.</param>
/// <param name="HiddenAt">When it was hidden.</param>
/// <param name="Title">Its title; <c>null</c> for a post without one or one that is gone.</param>
/// <param name="Gone">The post is no longer published (deleted or unpublished); it may still be shown again.</param>
public sealed record HiddenPostView(Guid PostId, DateTimeOffset HiddenAt, string? Title, bool Gone);

/// <summary>A topic hidden from the feeds.</summary>
/// <param name="BlogId">The hidden topic.</param>
/// <param name="HiddenAt">When it was hidden.</param>
/// <param name="Name">The topic's name; <c>null</c> until set or when the topic is gone.</param>
/// <param name="Nickname">The topic's short address without <c>@</c>; <c>null</c> until set or when it is gone.</param>
public sealed record HiddenTopicView(Guid BlogId, DateTimeOffset HiddenAt, string? Name, string? Nickname);

/// <summary>What the signed-in account hid from its feeds (E11-07).</summary>
/// <param name="Posts">The hidden posts, the latest hidden first.</param>
/// <param name="Topics">The hidden topics, the latest hidden first.</param>
/// <param name="Max">The most posts, and separately the most topics, the lists may hold.</param>
public sealed record HiddenListView(IReadOnlyList<HiddenPostView> Posts, IReadOnlyList<HiddenTopicView> Topics, int Max);

/// <summary>Whether the signed-in account hid one topic from its feeds.</summary>
/// <param name="Hidden">The topic is hidden.</param>
public sealed record HiddenTopicStateView(bool Hidden);

/// <summary>
/// The posts and topics the signed-in account hid from its feeds (E11-07, FR-SUB, FR-TOPIC, ADR-0119): read the lists,
/// hide a post or a topic and show it again. Every change may be repeated with the same result. A hidden post leaves
/// every feed of the person; a hidden topic leaves every feed but its own page. No one reads or changes another's lists.
/// </summary>
public sealed class HiddenEndpoints : IApiEndpoints
{
    /// <summary>The post to hide is not published: unknown, a draft or deleted.</summary>
    public const string PostNotFoundCode = "preferences.post_not_found";

    /// <summary>The topic to hide does not exist.</summary>
    public const string TopicNotFoundCode = "preferences.topic_not_found";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var own = v1.MapGroup("/preferences/me/hidden").WithTags("Preferences").RequireAuthorization();
        own.MapGet("/", GetAsync)
            .Produces<HiddenListView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetOwnHiddenList")
            .WithSummary("The posts and topics the signed-in account hid from its feeds, the latest hidden first");
        own.MapPut("/posts/{postId:guid}", HidePostAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("HidePost")
            .WithSummary("Hides a published post from the feeds of the signed-in account");
        own.MapDelete("/posts/{postId:guid}", ShowPostAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("ShowHiddenPost")
            .WithSummary("Brings a hidden post back to the feeds of the signed-in account");
        own.MapGet("/topics/{blogId:guid}", GetTopicAsync)
            .Produces<HiddenTopicStateView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetHiddenTopicState")
            .WithSummary("Whether the signed-in account hid a topic from its feeds");
        own.MapPut("/topics/{blogId:guid}", HideTopicAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("HideTopic")
            .WithSummary("Hides a topic from the feeds of the signed-in account");
        own.MapDelete("/topics/{blogId:guid}", ShowTopicAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("ShowHiddenTopic")
            .WithSummary("Brings a hidden topic back to the feeds of the signed-in account");
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        PreferencesDbContext db,
        IPostDirectory posts,
        IBlogDirectory blogs,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        var hiddenPosts = await db.HiddenPosts.AsNoTracking()
            .Where(h => h.OwnerAccountId == owner)
            .OrderByDescending(h => h.HiddenAt).ThenBy(h => h.PostId)
            .ToListAsync(ct);
        var hiddenTopics = await db.HiddenTopics.AsNoTracking()
            .Where(h => h.OwnerAccountId == owner)
            .OrderByDescending(h => h.HiddenAt).ThenBy(h => h.BlogId)
            .ToListAsync(ct);

        // The names come from the modules that own them, read at once for the whole list (ADR-0048, ADR-0052).
        IReadOnlyDictionary<Guid, PostHeadline> headlines = hiddenPosts.Count == 0
            ? new Dictionary<Guid, PostHeadline>()
            : await posts.PublishedHeadlinesAsync([.. hiddenPosts.Select(h => h.PostId)], ct);
        var cards = hiddenTopics.Count == 0
            ? []
            : (await blogs.CardsAsync([.. hiddenTopics.Select(h => h.BlogId)], [], ct)).ToDictionary(c => c.Id);

        return TypedResults.Ok(new HiddenListView(
            [
                .. hiddenPosts.Select(h => headlines.TryGetValue(h.PostId, out var headline)
                    ? new HiddenPostView(h.PostId, h.HiddenAt, headline.Title, Gone: false)
                    : new HiddenPostView(h.PostId, h.HiddenAt, Title: null, Gone: true)),
            ],
            [
                .. hiddenTopics.Select(h => cards.TryGetValue(h.BlogId, out var card)
                    ? new HiddenTopicView(h.BlogId, h.HiddenAt, card.Name, card.Nickname)
                    : new HiddenTopicView(h.BlogId, h.HiddenAt, Name: null, Nickname: null)),
            ],
            HiddenRules.MaxHidden));
    }

    private static async Task<IResult> HidePostAsync(
        Guid postId,
        ClaimsPrincipal user,
        PreferencesDbContext db,
        IPostDirectory posts,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        if (await db.HiddenPosts.AnyAsync(h => h.OwnerAccountId == owner && h.PostId == postId, ct))
        {
            return TypedResults.NoContent();
        }

        var count = await db.HiddenPosts.CountAsync(h => h.OwnerAccountId == owner, ct);
        if (HiddenRules.HidePostProblem(postId, count) is { } problem)
        {
            return Invalid("postId", problem);
        }

        if (await posts.FindPublishedAsync(postId, ct) is null)
        {
            return ApiProblems.NotFound(PostNotFoundCode);
        }

        db.HiddenPosts.Add(new HiddenPost { OwnerAccountId = owner, PostId = postId, HiddenAt = time.GetUtcNow() });
        await SaveOnceAsync(db, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ShowPostAsync(
        Guid postId, ClaimsPrincipal user, PreferencesDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        await db.HiddenPosts.Where(h => h.OwnerAccountId == owner && h.PostId == postId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetTopicAsync(
        Guid blogId, ClaimsPrincipal user, PreferencesDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        var hidden = await db.HiddenTopics.AnyAsync(h => h.OwnerAccountId == owner && h.BlogId == blogId, ct);
        return TypedResults.Ok(new HiddenTopicStateView(hidden));
    }

    private static async Task<IResult> HideTopicAsync(
        Guid blogId,
        ClaimsPrincipal user,
        PreferencesDbContext db,
        IBlogDirectory blogs,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        if (await db.HiddenTopics.AnyAsync(h => h.OwnerAccountId == owner && h.BlogId == blogId, ct))
        {
            return TypedResults.NoContent();
        }

        var count = await db.HiddenTopics.CountAsync(h => h.OwnerAccountId == owner, ct);
        if (HiddenRules.HideTopicProblem(blogId, count) is { } problem)
        {
            return Invalid("blogId", problem);
        }

        // Only a topic is hidden: a personal blog leaves the feeds through the black list of its author (E11-06).
        var blog = await blogs.FindAsync(blogId, ct);
        if (blog is null)
        {
            return ApiProblems.NotFound(TopicNotFoundCode);
        }

        if (blog.Kind != BlogKind.Topic)
        {
            return Invalid("blogId", "Скрыть можно только тему.");
        }

        db.HiddenTopics.Add(new HiddenTopic { OwnerAccountId = owner, BlogId = blogId, HiddenAt = time.GetUtcNow() });
        await SaveOnceAsync(db, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ShowTopicAsync(
        Guid blogId, ClaimsPrincipal user, PreferencesDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        await db.HiddenTopics.Where(h => h.OwnerAccountId == owner && h.BlogId == blogId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static IResult Invalid(string field, string problem) =>
        ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [problem] });

    private static async Task SaveOnceAsync(PreferencesDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        })
        {
            // Two devices hiding at the same moment: the other one already did it.
        }
    }

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
