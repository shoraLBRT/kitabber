namespace Platform.Modules.Blogs.Contracts;

/// <summary>
/// The owner of a topic offered it to another account (E11-12, ADR-0121): an internal event of the platform, published
/// through the outbox of the Blogs module in the same transaction as the offer. The Notifications module tells the
/// recipient, who accepts or declines the offer on the topic's management page. Accepting, declining and cancelling are
/// not announced.
/// </summary>
/// <param name="TopicId">The topic.</param>
/// <param name="FromAccountId">Its owner, who made the offer.</param>
/// <param name="ToAccountId">The account the topic is offered to.</param>
/// <param name="OccurredAt">When the offer was made: a new offer to the same person is a new notification.</param>
public sealed record TopicTransferOffered(Guid TopicId, Guid FromAccountId, Guid ToAccountId, DateTimeOffset OccurredAt);
