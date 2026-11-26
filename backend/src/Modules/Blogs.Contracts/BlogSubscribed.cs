namespace Platform.Modules.Blogs.Contracts;

/// <summary>
/// A person subscribed to a blog or a topic (ADR-0092): an internal event of the platform, published through the outbox
/// of the Blogs module in the same transaction as the subscription. A repeated subscribe changes nothing and is not
/// announced; unsubscribing is not announced either.
/// </summary>
/// <param name="BlogId">The blog or the topic.</param>
/// <param name="Kind">A personal blog or a topic.</param>
/// <param name="OwnerAccountId">The owner of the blog or of the topic; <c>null</c> for a topic without an owner.</param>
/// <param name="SubscriberAccountId">The person who subscribed.</param>
/// <param name="OccurredAt">When they subscribed.</param>
public sealed record BlogSubscribed(
    Guid BlogId,
    BlogKind Kind,
    Guid? OwnerAccountId,
    Guid SubscriberAccountId,
    DateTimeOffset OccurredAt);
