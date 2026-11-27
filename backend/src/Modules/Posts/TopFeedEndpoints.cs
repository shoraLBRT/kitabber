using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Posts;

/// <summary>
/// The periods of the top (FR-FEED, E08-06, ADR-0104): the posts published within the last day, week, month (30 days)
/// or year (365 days) before now, or all of them.
/// </summary>
public static class TopPeriods
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Year = "year";
    public const string All = "all";

    /// <summary>Every period, in the order of the switch on the site.</summary>
    public static readonly IReadOnlyList<string> Names = [Day, Week, Month, Year, All];

    /// <summary>
    /// How far back the period <paramref name="name"/> reaches from now; <c>null</c> for <see cref="All"/>. Throws for a
    /// name that is not a period.
    /// </summary>
    public static TimeSpan? Length(string name) => name switch
    {
        Day => TimeSpan.FromDays(1),
        Week => TimeSpan.FromDays(7),
        Month => TimeSpan.FromDays(30),
        Year => TimeSpan.FromDays(365),
        All => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a period of the top."),
    };

    /// <summary>Whether <paramref name="name"/> is a period (lower case, as in the address).</summary>
    public static bool IsPeriod(string? name) => name is not null && Names.Contains(name, StringComparer.Ordinal);
}

/// <summary>
/// The position in a top (ADR-0104): the rating and the id of the last post of a page. The top is sorted by both,
/// highest first, so the order is total.
/// </summary>
/// <param name="Rating">The rating of the last post of the page.</param>
/// <param name="Id">Its id, which breaks ties between equal ratings.</param>
public sealed record TopFeedCursor(int Rating, Guid Id);

/// <summary>
/// <c>/api/v1/feeds/top/{period}</c> (FR-FEED, E08-06, ADR-0104): the posts published within the period, by rating,
/// highest first, page by page with a cursor (ADR-0006). The rating is the one the hotness pass stored (ADR-0086), so a
/// post enters a top with the next pass. Each period's pages are cached like the other feeds (ADR-0080).
/// </summary>
public sealed class TopFeedEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/feeds").WithTags("Feeds")
            .MapGet("/top/{period}", GetAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("GetTopFeed")
            .WithSummary("Posts published within a period (day, week, month, year, all), by rating, page by page")
            .WithDescription(
                "`period` is one of `day`, `week`, `month` (30 days), `year` (365 days) or `all`, counted back from now.");
    }

    private static async Task<Ok<CursorPage<FeedPostView>>> GetAsync(
        string period,
        [AsParameters] PageQuery query,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        FeedPageCache cache,
        TimeProvider time,
        ClaimsPrincipal user,
        IEnumerable<IFeedExclusionSource> exclusionSources,
        CancellationToken ct)
    {
        if (!TopPeriods.IsPeriod(period))
        {
            var problem = new ApiProblemException(
                StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed, "Validation failed");
            problem.Extensions["errors"] = new Dictionary<string, string[]>
            {
                ["period"] = [$"The period must be one of: {string.Join(", ", TopPeriods.Names)}."],
            };
            throw problem;
        }

        var page = query.ToRequest<TopFeedCursor>();
        var position = page.After is { } after
            ? string.Create(CultureInfo.InvariantCulture, $"{after.Rating}:{after.Id:N}")
            : null;
        var since = TopPeriods.Length(period) is { } length ? time.GetUtcNow() - length : (DateTimeOffset?)null;
        // A top always exists, so the read is never null.
        var result = await cache.GetOrReadAsync(
            FeedPageCache.TopFeed(period),
            page.Limit,
            position,
            async token => await ReadAsync(page, since, db, media, blogs, comments, token),
            ct);
        // The reader's exclusions after the shared cache (ADR-0091).
        return TypedResults.Ok(
            ReaderExclusions.Apply(result!, await ReaderExclusions.ForAsync(user, exclusionSources, ct)));
    }

    private static async Task<CursorPage<FeedPostView>> ReadAsync(
        PageRequest<TopFeedCursor> page,
        DateTimeOffset? since,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct)
    {
        var published = db.Posts.AsNoTracking().Where(p => p.Status == PostStatus.Published && p.PublishedAt != null);
        if (since is { } from)
        {
            published = published.Where(p => p.PublishedAt >= from);
        }

        var ranked = db.Hotness.AsNoTracking().Join(
            published,
            h => h.PostId,
            p => p.Id,
            (h, p) => new { h.Rating, Post = p });
        if (page.After is { } after)
        {
            // (rating, id) < (@rating, @id)
            ranked = ranked.Where(r => EF.Functions.LessThan(
                ValueTuple.Create(r.Rating, r.Post.Id), ValueTuple.Create(after.Rating, after.Id)));
        }

        var rows = await ranked
            .OrderByDescending(r => r.Rating).ThenByDescending(r => r.Post.Id)
            .Take(page.FetchCount)
            .Select(r => new Ranked(r.Rating, r.Post))
            .ToListAsync(ct);
        var result = page.ToPage(rows, r => new TopFeedCursor(r.Rating, r.Post.Id));
        var views = await PostEndpoints.ViewsAsync([.. result.Items.Select(r => r.Post)], media, ct);
        return new CursorPage<FeedPostView>(
            await FeedPostViews.BuildAsync(views, blogs, comments, ct), result.NextCursor);
    }

    private sealed record Ranked(int Rating, Post Post);
}
