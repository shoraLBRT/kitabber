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

    public async Task<IReadOnlySet<Guid>> SubscribedAmongAsync(
        Guid accountId, IReadOnlyCollection<Guid> blogIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blogIds);
        if (blogIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = await db.Subscriptions.AsNoTracking()
            .Where(s => s.AccountId == accountId && blogIds.Contains(s.SubsiteId))
            .Select(s => s.SubsiteId)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public async Task<IReadOnlyList<Guid>> NewPostRecipientsAsync(
        Guid blogId, Guid authorAccountId, CancellationToken cancellationToken)
    {
        // The blog of the post, and the author's personal blog: a personal blog's posts are its owner's posts (ADR-0089).
        var blogs = db.Subsites
            .Where(b => b.Id == blogId || (b.Type == SubsiteType.Personal && b.OwnerAccountId == authorAccountId))
            .Select(b => b.Id);
        return await db.Subscriptions.AsNoTracking()
            .Where(s => s.NotifyNewPosts && blogs.Contains(s.SubsiteId) && s.AccountId != authorAccountId)
            .Select(s => s.AccountId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}

/// <summary>The subscriber numbers of a blog as its profile shows them (ADR-0085).</summary>
/// <param name="SubscriberCount">How many accounts follow the blog.</param>
/// <param name="SubscriptionCount">How many blogs and topics the owner of a personal blog follows; <c>null</c> for a topic.</param>
/// <param name="Subscribed">The reader follows the blog; <c>false</c> for a guest.</param>
/// <param name="NotifyNewPosts">The reader is notified about the blog's new posts (ADR-0118).</param>
internal sealed record SubscriptionNumbers(int SubscriberCount, int? SubscriptionCount, bool Subscribed, bool NotifyNewPosts);

/// <summary>Reads the numbers straight from the rows, so they are always exact (ADR-0085).</summary>
internal static class SubscriptionCounts
{
    public static async Task<SubscriptionNumbers> OfAsync(BlogsDbContext db, Subsite blog, Guid? viewer, CancellationToken ct)
    {
        var subscribers = await db.Subscriptions.CountAsync(s => s.SubsiteId == blog.Id, ct);
        int? subscriptions = blog.Type != SubsiteType.Personal ? null
            : blog.OwnerAccountId is { } owner ? await db.Subscriptions.CountAsync(s => s.AccountId == owner, ct)
            : 0;
        var own = viewer is { } account
            ? await db.Subscriptions.AsNoTracking()
                .Where(s => s.SubsiteId == blog.Id && s.AccountId == account)
                .Select(s => new { s.NotifyNewPosts })
                .SingleOrDefaultAsync(ct)
            : null;
        return new SubscriptionNumbers(subscribers, subscriptions, own is not null, own?.NotifyNewPosts ?? false);
    }
}
