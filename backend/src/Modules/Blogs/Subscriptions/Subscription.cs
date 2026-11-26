namespace Platform.Modules.Blogs.Subscriptions;

/// <summary>
/// An account follows a blog or a topic (E11-03, FR-SUB, ADR-0085). The key is the account and the blog, so subscribing
/// again is the same subscription; the subscriber is known only by the account_id (FR-ID-1).
/// </summary>
public sealed class Subscription
{
    public Guid AccountId { get; init; }

    public Guid SubsiteId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public static Subscription Create(Guid accountId, Guid subsiteId, DateTimeOffset now)
    {
        if (accountId == Guid.Empty || subsiteId == Guid.Empty)
        {
            throw new ArgumentException("A subscription needs the account and the blog.");
        }

        return new Subscription { AccountId = accountId, SubsiteId = subsiteId, CreatedAt = now };
    }
}

/// <summary>Why a subscription is refused (ADR-0085).</summary>
public enum SubscriptionRefusal
{
    /// <summary>The account would follow its own personal blog: no one subscribes to themselves.</summary>
    OwnBlog = 1,
}

/// <summary>The rules of a subscription, apart from the database: the endpoint applies them before it writes.</summary>
public static class SubscriptionRules
{
    /// <summary>
    /// Why <paramref name="account"/> may not subscribe to the blog of type <paramref name="type"/> owned by
    /// <paramref name="ownerAccountId"/>, or <c>null</c> when it may. The owner of a topic may follow their own topic.
    /// </summary>
    public static SubscriptionRefusal? Refusal(Guid account, SubsiteType type, Guid? ownerAccountId) =>
        type == SubsiteType.Personal && ownerAccountId == account ? SubscriptionRefusal.OwnBlog : null;
}
