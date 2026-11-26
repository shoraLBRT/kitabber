using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Http.RateLimiting;

namespace Platform.Modules.Posts;

/// <summary>The cards of a feed the reader has seen.</summary>
/// <param name="PostIds">The posts whose cards were seen, at most 50 at once.</param>
public sealed record ImpressionsInput(IReadOnlyList<Guid>? PostIds);

/// <summary>How long the reader has read a post's page.</summary>
/// <param name="Seconds">The time the page was visible, in whole seconds, from 1 to 7200.</param>
public sealed record ReadingInput(int Seconds);

/// <summary>
/// The view counter of posts (E07-06, FR-POST, ADR-0071): the site reports from script that a post's page was opened,
/// that cards were seen in a feed and how long a page was read. A viewer — the account, or a guest's pseudonym — counts
/// once a day; bots and the author are never counted. Every report answers 204 whether or not it counted, so a
/// script cannot learn the rules by trying. Guests report too: no cookie is set and nothing is kept in the browser.
/// </summary>
public sealed class PostViewEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        var posts = v1.MapGroup("/posts").WithTags("Views");
        posts.MapPost("/{id:guid}/open", OpenAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("RecordPostOpen")
            .WithSummary("Counts the opening of a published post's page, once a day per reader");
        posts.MapPost("/impressions", ImpressionsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .WithName("RecordPostImpressions")
            .WithSummary("Counts the feed cards the reader has seen, each once a day per reader");
        posts.MapPost("/{id:guid}/reading", ReadingAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("RecordPostReading")
            .WithSummary("Keeps how long the reader read a post's page they opened today");
    }

    private static async Task<IResult> OpenAsync(
        Guid id, HttpContext context, PostsDbContext db, IViewCounter counter, TimeProvider time, CancellationToken ct)
    {
        if (!await IsPublishedAsync(db, id, ct))
        {
            return ApiProblems.NotFound(PostEndpoints.PostNotFoundCode);
        }

        if (await ViewerAsync(context, counter, time, ct) is { } viewer)
        {
            await counter.CountOpenAsync(id, viewer.Key, viewer.Account, time.GetUtcNow(), ct);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ImpressionsAsync(
        ImpressionsInput input, HttpContext context, IViewCounter counter, TimeProvider time, CancellationToken ct)
    {
        if (input.PostIds is not { Count: > 0 and <= ViewRules.MaxImpressions } ids)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["postIds"] = [$"Нужно от 1 до {ViewRules.MaxImpressions} постов."],
            });
        }

        if (await ViewerAsync(context, counter, time, ct) is { } viewer)
        {
            await counter.CountImpressionsAsync(ids, viewer.Key, viewer.Account, time.GetUtcNow(), ct);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ReadingAsync(
        Guid id,
        ReadingInput input,
        HttpContext context,
        PostsDbContext db,
        IViewCounter counter,
        TimeProvider time,
        CancellationToken ct)
    {
        if (input.Seconds is < 1 or > ViewRules.MaxReadSeconds)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["seconds"] = [$"Время чтения — от 1 до {ViewRules.MaxReadSeconds} секунд."],
            });
        }

        if (!await IsPublishedAsync(db, id, ct))
        {
            return ApiProblems.NotFound(PostEndpoints.PostNotFoundCode);
        }

        if (await ViewerAsync(context, counter, time, ct) is { } viewer)
        {
            await counter.RecordReadingAsync(id, viewer.Key, input.Seconds, time.GetUtcNow(), ct);
        }

        return TypedResults.NoContent();
    }

    private static Task<bool> IsPublishedAsync(PostsDbContext db, Guid id, CancellationToken ct) =>
        db.Posts.AsNoTracking().AnyAsync(p => p.Id == id && p.Status == PostStatus.Published, ct);

    // Who is viewing: the account, or the guest's pseudonym for today; null for a bot, which is never counted.
    private static async Task<(string Key, Guid? Account)?> ViewerAsync(
        HttpContext context, IViewCounter counter, TimeProvider time, CancellationToken ct)
    {
        var headers = context.Request.Headers;
        var purpose = headers["Sec-Purpose"].ToString() + headers["Purpose"].ToString();
        var userAgent = headers.UserAgent.ToString();
        if (ViewRules.IsBot(userAgent, purpose))
        {
            return null;
        }

        if (AccountOf(context.User) is { } account)
        {
            return (ViewRules.AccountViewer(account), account);
        }

        var salt = await counter.SaltAsync(time.GetUtcNow(), ct);
        return (ViewRules.GuestViewer(salt, ClientAddress.Network(context.Connection.RemoteIpAddress), userAgent), null);
    }

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
