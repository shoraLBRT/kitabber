using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;
using Platform.Modules.Blogs.Profiles;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Blogs.Subscriptions;

/// <summary>A row of a subscriber or subscription list (E04-06, ADR-0099): a blog or a topic and the reader's own mark.</summary>
/// <param name="Id">The blog's identifier: a subscriber is shown by their personal blog.</param>
/// <param name="Type"><c>personal</c> or <c>topic</c>.</param>
/// <param name="Name">Its name; <c>null</c> until the owner sets it.</param>
/// <param name="Nickname">Its short address without <c>@</c>; <c>null</c> while it has none.</param>
/// <param name="AvatarSources">
/// The avatar for every screen density (ADR-0040), as the profile gives it; empty when there is no avatar.
/// </param>
/// <param name="Subscribed">The reader follows this blog; <c>false</c> for a guest.</param>
/// <param name="Own">The blog is the reader's own personal blog: the row has no «Подписаться».</param>
public sealed record SubscriptionCard(
    Guid Id,
    string Type,
    string? Name,
    string? Nickname,
    IReadOnlyList<ImageSource> AvatarSources,
    bool Subscribed,
    bool Own);

/// <summary>The position after the last row of a list page: when the subscription was made and its other key.</summary>
/// <param name="SubscribedAt">When the subscription of the last row was made.</param>
/// <param name="Key">The subscriber's account (subscribers) or the followed blog (subscriptions).</param>
public sealed record SubscriptionListCursor(DateTimeOffset SubscribedAt, Guid Key);

/// <summary>
/// <c>/api/v1/blogs/{id}/subscribers</c> and <c>/api/v1/blogs/{id}/subscriptions</c> (E04-06, FR-PROFILE, ADR-0099), open
/// to everyone like the numbers of the header: who follows a blog or a topic, and which blogs and topics the owner of a
/// personal blog follows, the newest subscriptions first, page by page with a cursor (ADR-0006). Each list holds exactly
/// the rows the header counts (ADR-0085).
/// </summary>
public sealed class SubscriptionListEndpoints : IApiEndpoints
{
    public const string NoSubscriptionsCode = "blogs.topic_has_no_subscriptions";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var blog = v1.MapGroup("/blogs/{id:guid}").WithTags("Subscriptions");
        blog.MapGet("/subscribers", SubscribersAsync)
            .Produces<CursorPage<SubscriptionCard>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetBlogSubscribers")
            .WithSummary("The accounts that follow a blog or a topic, by their personal blogs, the newest first");
        blog.MapGet("/subscriptions", SubscriptionsAsync)
            .Produces<CursorPage<SubscriptionCard>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetBlogSubscriptions")
            .WithSummary("The blogs and topics the owner of a personal blog follows, the newest first");
    }

    // Member-initialised, not positional: EF composes the cursor and the order over it in SQL.
    private sealed class Row
    {
        public DateTimeOffset SubscribedAt { get; init; }

        public Guid Key { get; init; }

        public Subsite Blog { get; init; } = null!;
    }

    private static async Task<IResult> SubscribersAsync(
        Guid id,
        [AsParameters] PageQuery query,
        ClaimsPrincipal user,
        BlogsDbContext db,
        IMediaLibrary media,
        CancellationToken ct)
    {
        var page = query.ToRequest<SubscriptionListCursor>();
        if (!await db.Subsites.AnyAsync(s => s.Id == id, ct))
        {
            return ApiProblems.NotFound(SubscriptionEndpoints.BlogNotFoundCode);
        }

        // A subscriber is shown by their personal blog: every account has one from its creation (ADR-0099).
        var rows = db.Subscriptions.AsNoTracking()
            .Where(s => s.SubsiteId == id)
            .Join(
                db.Subsites.Where(b => b.Type == SubsiteType.Personal),
                s => s.AccountId,
                b => b.OwnerAccountId,
                (s, b) => new Row { SubscribedAt = s.CreatedAt, Key = s.AccountId, Blog = b });
        return TypedResults.Ok(await PageAsync(rows, page, AccountOf(user), db, media, ct));
    }

    private static async Task<IResult> SubscriptionsAsync(
        Guid id,
        [AsParameters] PageQuery query,
        ClaimsPrincipal user,
        BlogsDbContext db,
        IMediaLibrary media,
        CancellationToken ct)
    {
        var page = query.ToRequest<SubscriptionListCursor>();
        if (await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) is not { } blog)
        {
            return ApiProblems.NotFound(SubscriptionEndpoints.BlogNotFoundCode);
        }

        // A topic follows no one: its profile has no subscription count either (ADR-0085).
        if (blog.Type != SubsiteType.Personal || blog.OwnerAccountId is not { } owner)
        {
            return ApiProblems.NotFound(NoSubscriptionsCode, "У темы нет подписок.");
        }

        var rows = db.Subscriptions.AsNoTracking()
            .Where(s => s.AccountId == owner)
            .Join(
                db.Subsites,
                s => s.SubsiteId,
                b => b.Id,
                (s, b) => new Row { SubscribedAt = s.CreatedAt, Key = s.SubsiteId, Blog = b });
        return TypedResults.Ok(await PageAsync(rows, page, AccountOf(user), db, media, ct));
    }

    // The newest subscriptions first; among equal moments the larger key first, so the order is complete (ADR-0006).
    private static async Task<CursorPage<SubscriptionCard>> PageAsync(
        IQueryable<Row> rows,
        PageRequest<SubscriptionListCursor> page,
        Guid? viewer,
        BlogsDbContext db,
        IMediaLibrary media,
        CancellationToken ct)
    {
        if (page.After is { } after)
        {
            rows = rows.Where(r => EF.Functions.LessThan(
                ValueTuple.Create(r.SubscribedAt, r.Key), ValueTuple.Create(after.SubscribedAt, after.Key)));
        }

        var fetched = await rows
            .OrderByDescending(r => r.SubscribedAt).ThenByDescending(r => r.Key)
            .Take(page.FetchCount)
            .ToListAsync(ct);

        var blogIds = fetched.Select(r => r.Blog.Id).ToList();
        var followed = viewer is { } account
            ? (await db.Subscriptions.AsNoTracking()
                .Where(s => s.AccountId == account && blogIds.Contains(s.SubsiteId))
                .Select(s => s.SubsiteId)
                .ToListAsync(ct)).ToHashSet()
            : [];
        var avatarIds = fetched.Select(r => r.Blog.AvatarMediaId).OfType<Guid>().Distinct().ToList();
        var avatars = avatarIds.Count > 0
            ? await media.ImageSourcesAsync(avatarIds, ProfileEndpoints.AvatarSourceVariants, ct)
            : new Dictionary<Guid, IReadOnlyList<ImageSource>>().AsReadOnly();

        var rowsByCard = fetched.ToDictionary(r => r.Blog.Id);
        return page.ToPage(
            [
                .. fetched.Select(r => new SubscriptionCard(
                    r.Blog.Id,
                    r.Blog.Type == SubsiteType.Personal ? "personal" : "topic",
                    r.Blog.Name,
                    r.Blog.Nickname,
                    r.Blog.AvatarMediaId is { } avatar && avatars.TryGetValue(avatar, out var sources) ? sources : [],
                    followed.Contains(r.Blog.Id),
                    viewer is not null && r.Blog.Type == SubsiteType.Personal && r.Blog.OwnerAccountId == viewer)),
            ],
            card => new SubscriptionListCursor(rowsByCard[card.Id].SubscribedAt, rowsByCard[card.Id].Key));
    }

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
