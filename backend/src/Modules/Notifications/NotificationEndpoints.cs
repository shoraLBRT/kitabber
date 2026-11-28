using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Notifications;

/// <summary>The person who acted, as a notification's row shows them: their personal blog (ADR-0093).</summary>
/// <param name="AccountId">The account of the person.</param>
/// <param name="BlogId">Their personal blog, the address of the row; <c>null</c> when they have none yet.</param>
/// <param name="Name">The blog's name; <c>null</c> until the owner sets it or when the person has no blog yet.</param>
/// <param name="Nickname">The short address without <c>@</c>; <c>null</c> until the owner sets it.</param>
/// <param name="AvatarSources">The avatar for every screen density, narrowest first; empty without an avatar.</param>
public sealed record NotificationActorView(
    Guid AccountId, Guid? BlogId, string? Name, string? Nickname, IReadOnlyList<ImageSource> AvatarSources);

/// <summary>A notification in the feed of the signed-in account (E12-02, FR-NOTIFY, ADR-0093).</summary>
/// <param name="Id">The notification's identifier; the client marks it read by it.</param>
/// <param name="Type">
/// <c>comment_reply</c>, <c>post_comment</c>, <c>reaction</c>, <c>mention</c>, <c>new_subscriber</c>, <c>repost</c>, <c>subscription_post</c> or <c>topic_transfer_offer</c>; the set grows
/// with the events of the spec, so a client shows an unknown type as a plain row.
/// </param>
/// <param name="Actor">Who acted.</param>
/// <param name="PostId">The post the action is about or under; <c>null</c> for a subscription.</param>
/// <param name="CommentId">The comment the action is or is about; <c>null</c> when it is about a post or a blog.</param>
/// <param name="ReactionKind">The reaction (<c>like</c>); <c>null</c> for the other types.</param>
/// <param name="CreatedAt">When the action happened.</param>
/// <param name="Read">Whether the recipient has seen it.</param>
/// <param name="BlogId">
/// The blog or the topic the action is about: the subscribed blog, the blog of a new post, the topic offered to the
/// recipient (E11-12); <c>null</c> for the other types.
/// </param>
public sealed record NotificationView(
    Guid Id,
    string Type,
    NotificationActorView Actor,
    Guid? PostId,
    Guid? CommentId,
    string? ReactionKind,
    DateTimeOffset CreatedAt,
    bool Read,
    Guid? BlogId = null);

/// <summary>The number of the signed-in account's unread notifications: the counter of the header.</summary>
/// <param name="Unread">How many notifications are unread.</param>
public sealed record UnreadNotificationsView(int Unread);

/// <summary>The notifications the client has shown, to be marked read.</summary>
/// <param name="Ids">The identifiers, 1 to <see cref="NotificationEndpoints.MaxMarked"/>; another's are ignored.</param>
public sealed record MarkNotificationsReadRequest(IReadOnlyList<Guid>? Ids);

/// <summary>Where a page of the notification feed ends: the newest first, then the larger id (ADR-0006).</summary>
public sealed record NotificationCursor(DateTimeOffset CreatedAt, Guid Id);

/// <summary>
/// The read side of the notifications in the API host (E12-02, FR-NOTIFY, ADR-0093): the feed of the signed-in account,
/// newest first, page by page with a cursor; the number of the unread ones for the header's counter; and marking the
/// shown ones read, which answers with the new number. No one reads or marks another's notifications. The person who
/// acted is read from Blogs (<see cref="IBlogDirectory"/>) for a whole page at once.
/// </summary>
public sealed class NotificationEndpoints : IApiEndpoints
{
    /// <summary>The most notifications one request marks read: a page of the feed at most.</summary>
    public const int MaxMarked = PageQuery.MaxLimit;

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var own = v1.MapGroup("/notifications/me").WithTags("Notifications").RequireAuthorization();
        own.MapGet("/", GetFeedAsync)
            .Produces<CursorPage<NotificationView>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetOwnNotifications")
            .WithSummary("The notifications of the signed-in account, newest first, page by page with a cursor");
        own.MapGet("/unread", GetUnreadAsync)
            .Produces<UnreadNotificationsView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetOwnUnreadNotifications")
            .WithSummary("The number of the unread notifications of the signed-in account");
        own.MapPost("/read", MarkReadAsync)
            .Produces<UnreadNotificationsView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("MarkOwnNotificationsRead")
            .WithSummary("Marks the shown notifications of the signed-in account read and gives the unread number");
    }

    /// <summary>The name a type has in the API.</summary>
    public static string TypeName(NotificationType type) => type switch
    {
        NotificationType.CommentReply => "comment_reply",
        NotificationType.PostComment => "post_comment",
        NotificationType.Reaction => "reaction",
        NotificationType.Mention => "mention",
        NotificationType.NewSubscriber => "new_subscriber",
        NotificationType.Repost => "repost",
        NotificationType.SubscriptionPost => "subscription_post",
        NotificationType.TopicTransferOffer => "topic_transfer_offer",
        _ => type.ToString().ToLowerInvariant(),
    };

    /// <summary>The problem of a mark-read request, or <c>null</c> when it is valid.</summary>
    public static string? MarkProblem(IReadOnlyList<Guid>? ids) => ids switch
    {
        null or { Count: 0 } => "Name at least one notification.",
        { Count: > MaxMarked } => $"At most {MaxMarked} notifications are marked at once.",
        _ => null,
    };

    private static async Task<IResult> GetFeedAsync(
        [AsParameters] PageQuery query,
        ClaimsPrincipal user,
        HttpContext http,
        NotificationsDbContext db,
        IBlogDirectory blogs,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } recipient)
        {
            return TypedResults.Unauthorized();
        }

        var page = query.ToRequest<NotificationCursor>();
        http.Response.Headers.CacheControl = "private, no-store";
        var rows = db.Notifications.AsNoTracking().Where(n => n.RecipientAccountId == recipient);
        if (page.After is { } after)
        {
            rows = rows.Where(n => EF.Functions.LessThan(
                ValueTuple.Create(n.CreatedAt, n.Id), ValueTuple.Create(after.CreatedAt, after.Id)));
        }

        var found = await rows
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(page.FetchCount)
            .ToListAsync(ct);
        var result = page.ToPage(found, n => new NotificationCursor(n.CreatedAt, n.Id));
        var cards = await blogs.CardsAsync([], [.. result.Items.Select(n => n.ActorAccountId).Distinct()], ct);
        var byOwner = cards
            .Where(c => c.Kind == BlogKind.Personal && c.OwnerAccountId is not null)
            .GroupBy(c => c.OwnerAccountId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var views = result.Items.Select(n => new NotificationView(
            n.Id,
            TypeName(n.Type),
            byOwner.TryGetValue(n.ActorAccountId, out var card)
                ? new NotificationActorView(n.ActorAccountId, card.Id, card.Name, card.Nickname, card.AvatarSources)
                : new NotificationActorView(n.ActorAccountId, null, null, null, []),
            n.PostId,
            n.CommentId,
            n.ReactionKind,
            n.CreatedAt,
            n.ReadAt is not null,
            n.BlogId)).ToList();
        return TypedResults.Ok(new CursorPage<NotificationView>(views, result.NextCursor));
    }

    private static async Task<IResult> GetUnreadAsync(
        ClaimsPrincipal user, HttpContext http, NotificationsDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } recipient)
        {
            return TypedResults.Unauthorized();
        }

        http.Response.Headers.CacheControl = "private, no-store";
        return TypedResults.Ok(new UnreadNotificationsView(await UnreadAsync(db, recipient, ct)));
    }

    private static async Task<IResult> MarkReadAsync(
        MarkNotificationsReadRequest request,
        ClaimsPrincipal user,
        NotificationsDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } recipient)
        {
            return TypedResults.Unauthorized();
        }

        if (MarkProblem(request.Ids) is { } problem)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["ids"] = [problem],
            });
        }

        var ids = request.Ids!.Distinct().ToList();
        var now = time.GetUtcNow();
        // Only the recipient's own unread ones: another's id and a repeated mark change nothing.
        await db.Notifications
            .Where(n => n.RecipientAccountId == recipient && n.ReadAt == null && ids.Contains(n.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAt, now), ct);
        return TypedResults.Ok(new UnreadNotificationsView(await UnreadAsync(db, recipient, ct)));
    }

    private static Task<int> UnreadAsync(NotificationsDbContext db, Guid recipient, CancellationToken ct) =>
        db.Notifications.CountAsync(n => n.RecipientAccountId == recipient && n.ReadAt == null, ct);

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
