using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Messaging;
using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Blogs.Subscriptions;

/// <summary>The subscription of the signed-in account to a blog, after a change (ADR-0085).</summary>
/// <param name="SubscriberCount">How many accounts follow the blog now.</param>
/// <param name="Subscribed">The account follows the blog now.</param>
/// <param name="NotifyNewPosts">
/// The account is notified about the blog's new posts (E11-04, ADR-0118); <c>false</c> when it does not follow the blog.
/// </param>
public sealed record SubscriptionView(int SubscriberCount, bool Subscribed, bool NotifyNewPosts = false);

/// <summary>
/// <c>/api/v1/blogs/{id}/subscription</c> (E11-03, FR-SUB, ADR-0085): the signed-in account subscribes to a blog or a
/// topic and unsubscribes, both idempotent; no one subscribes to their own personal blog. The numbers and the reader's
/// own mark come with the profile (<c>/api/v1/profiles</c>). <c>…/subscription/notifications</c> (E11-04, ADR-0118)
/// turns the «колокольчик» on and off: the setting lives on the subscription, so only a subscriber has one.
/// </summary>
public sealed class SubscriptionEndpoints : IApiEndpoints
{
    public const string BlogNotFoundCode = "blogs.blog_not_found";
    public const string OwnBlogCode = "blogs.own_blog";
    public const string NotSubscribedCode = "blogs.not_subscribed";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var blog = v1.MapGroup("/blogs/{id:guid}").WithTags("Subscriptions");
        blog.MapPut("/subscription", SubscribeAsync)
            .RequireAuthorization()
            .Produces<SubscriptionView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("SubscribeToBlog")
            .WithSummary("Subscribes to a blog or a topic; subscribing again changes nothing");
        blog.MapDelete("/subscription", UnsubscribeAsync)
            .RequireAuthorization()
            .Produces<SubscriptionView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("UnsubscribeFromBlog")
            .WithSummary("Unsubscribes from a blog or a topic; unsubscribing when not subscribed changes nothing");
        blog.MapPut("/subscription/notifications", (Guid id, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct)
                => SetNotificationsAsync(id, true, user, db, ct))
            .RequireAuthorization()
            .Produces<SubscriptionView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("TurnOnNewPostNotifications")
            .WithSummary("Turns on the notifications about new posts of a subscribed blog or topic; idempotent");
        blog.MapDelete("/subscription/notifications", (Guid id, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct)
                => SetNotificationsAsync(id, false, user, db, ct))
            .RequireAuthorization()
            .Produces<SubscriptionView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("TurnOffNewPostNotifications")
            .WithSummary("Turns off the notifications about new posts of a subscribed blog or topic; idempotent");
    }

    // The key is the account and the blog: a subscription already there, even one written by a request at the same
    // moment, is the same subscription (ADR-0085).
    private static async Task<IResult> SubscribeAsync(
        Guid id,
        ClaimsPrincipal user,
        BlogsDbContext db,
        IModuleOutbox<BlogsDbContext> outbox,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) is not { } blog)
        {
            return ApiProblems.NotFound(BlogNotFoundCode);
        }

        if (SubscriptionRules.Refusal(account, blog.Type, blog.OwnerAccountId) is SubscriptionRefusal.OwnBlog)
        {
            return ApiProblems.Forbidden(OwnBlogCode, "На свой блог подписаться нельзя.");
        }

        if (!await db.Subscriptions.AnyAsync(s => s.AccountId == account && s.SubsiteId == id, ct))
        {
            var now = time.GetUtcNow();
            db.Subscriptions.Add(Subscription.Create(account, id, now));
            // Only a new subscription is announced, in its own transaction (ADR-0092); a lost race clears both.
            await outbox.Publish(
                new BlogSubscribed(
                    id,
                    blog.Type == SubsiteType.Personal ? BlogKind.Personal : BlogKind.Topic,
                    blog.OwnerAccountId,
                    account,
                    now),
                ct);
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

        return TypedResults.Ok(await StateAsync(db, id, account, ct));
    }

    private static async Task<IResult> UnsubscribeAsync(
        Guid id, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (!await db.Subsites.AnyAsync(s => s.Id == id, ct))
        {
            return ApiProblems.NotFound(BlogNotFoundCode);
        }

        await db.Subscriptions.Where(s => s.AccountId == account && s.SubsiteId == id).ExecuteDeleteAsync(ct);
        return TypedResults.Ok(await StateAsync(db, id, account, ct));
    }

    // The setting is on the subscription (ADR-0118): without one there is nothing to turn on or off.
    private static async Task<IResult> SetNotificationsAsync(
        Guid id, bool on, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (!await db.Subsites.AnyAsync(s => s.Id == id, ct))
        {
            return ApiProblems.NotFound(BlogNotFoundCode);
        }

        if (await db.Subscriptions.SingleOrDefaultAsync(s => s.AccountId == account && s.SubsiteId == id, ct)
            is not { } subscription)
        {
            return ApiProblems.Conflict(NotSubscribedCode, "Уведомления о постах включаются только у подписки.");
        }

        subscription.SetNewPostNotifications(on);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await StateAsync(db, id, account, ct));
    }

    private static async Task<SubscriptionView> StateAsync(BlogsDbContext db, Guid blogId, Guid account, CancellationToken ct)
    {
        var own = await db.Subscriptions.AsNoTracking()
            .Where(s => s.SubsiteId == blogId && s.AccountId == account)
            .Select(s => new { s.NotifyNewPosts })
            .SingleOrDefaultAsync(ct);
        return new SubscriptionView(
            await db.Subscriptions.CountAsync(s => s.SubsiteId == blogId, ct),
            own is not null,
            own?.NotifyNewPosts ?? false);
    }

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
