using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Http;
using Platform.Http.Problems;

namespace Platform.Modules.Blogs.Subscriptions;

/// <summary>The subscription of the signed-in account to a blog, after a change (ADR-0085).</summary>
/// <param name="SubscriberCount">How many accounts follow the blog now.</param>
/// <param name="Subscribed">The account follows the blog now.</param>
public sealed record SubscriptionView(int SubscriberCount, bool Subscribed);

/// <summary>
/// <c>/api/v1/blogs/{id}/subscription</c> (E11-03, FR-SUB, ADR-0085): the signed-in account subscribes to a blog or a
/// topic and unsubscribes, both idempotent; no one subscribes to their own personal blog. The numbers and the reader's
/// own mark come with the profile (<c>/api/v1/profiles</c>).
/// </summary>
public sealed class SubscriptionEndpoints : IApiEndpoints
{
    public const string BlogNotFoundCode = "blogs.blog_not_found";
    public const string OwnBlogCode = "blogs.own_blog";

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
    }

    // The key is the account and the blog: a subscription already there, even one written by a request at the same
    // moment, is the same subscription (ADR-0085).
    private static async Task<IResult> SubscribeAsync(
        Guid id, ClaimsPrincipal user, BlogsDbContext db, TimeProvider time, CancellationToken ct)
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
            db.Subscriptions.Add(Subscription.Create(account, id, time.GetUtcNow()));
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

    private static async Task<SubscriptionView> StateAsync(BlogsDbContext db, Guid blogId, Guid account, CancellationToken ct) =>
        new(
            await db.Subscriptions.CountAsync(s => s.SubsiteId == blogId, ct),
            await db.Subscriptions.AnyAsync(s => s.SubsiteId == blogId && s.AccountId == account, ct));

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
