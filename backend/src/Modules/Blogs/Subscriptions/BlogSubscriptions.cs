using Microsoft.EntityFrameworkCore;
using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Blogs.Subscriptions;

/// <summary><see cref="IBlogSubscriptions"/> over the schema <c>blogs</c> (ADR-0085).</summary>
internal sealed class BlogSubscriptions(BlogsDbContext db) : IBlogSubscriptions
{
    public async Task<IReadOnlyList<BlogSummary>> SubscribedBlogsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var rows = await db.Subscriptions.AsNoTracking()
            .Where(s => s.AccountId == accountId)
            .Join(db.Subsites, s => s.SubsiteId, b => b.Id, (s, b) => new { s.CreatedAt, b.Id, b.Type, b.OwnerAccountId })
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return
        [
            .. rows.Select(x => new BlogSummary(
                x.Id, x.Type == SubsiteType.Personal ? BlogKind.Personal : BlogKind.Topic, x.OwnerAccountId)),
        ];
    }

    public Task<bool> IsSubscribedAsync(Guid accountId, Guid blogId, CancellationToken cancellationToken) =>
        db.Subscriptions.AnyAsync(s => s.AccountId == accountId && s.SubsiteId == blogId, cancellationToken);
}

/// <summary>The subscriber numbers of a blog as its profile shows them (ADR-0085).</summary>
/// <param name="SubscriberCount">How many accounts follow the blog.</param>
/// <param name="SubscriptionCount">How many blogs and topics the owner of a personal blog follows; <c>null</c> for a topic.</param>
/// <param name="Subscribed">The reader follows the blog; <c>false</c> for a guest.</param>
internal sealed record SubscriptionNumbers(int SubscriberCount, int? SubscriptionCount, bool Subscribed);

/// <summary>Reads the numbers straight from the rows, so they are always exact (ADR-0085).</summary>
internal static class SubscriptionCounts
{
    public static async Task<SubscriptionNumbers> OfAsync(BlogsDbContext db, Subsite blog, Guid? viewer, CancellationToken ct)
    {
        var subscribers = await db.Subscriptions.CountAsync(s => s.SubsiteId == blog.Id, ct);
        int? subscriptions = blog.Type != SubsiteType.Personal ? null
            : blog.OwnerAccountId is { } owner ? await db.Subscriptions.CountAsync(s => s.AccountId == owner, ct)
            : 0;
        var subscribed = viewer is { } account
            && await db.Subscriptions.AnyAsync(s => s.SubsiteId == blog.Id && s.AccountId == account, ct);
        return new SubscriptionNumbers(subscribers, subscriptions, subscribed);
    }
}
