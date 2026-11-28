namespace Platform.Modules.Posts.Contracts;

/// <summary>
/// A post was published (E11-04, ADR-0118): an internal event of the platform, published through the outbox of the Posts
/// module in the same transaction as the publishing. Only the first publishing of a post is announced; a repost is not
/// (it tells the original's author with <see cref="PostReposted"/>). The Notifications module tells the subscribers
/// who turned on the notifications about new posts.
/// </summary>
/// <param name="PostId">The post.</param>
/// <param name="BlogId">The blog or the topic it is in.</param>
/// <param name="AuthorAccountId">Its author.</param>
/// <param name="OccurredAt">When it was published.</param>
public sealed record PostPublished(Guid PostId, Guid BlogId, Guid AuthorAccountId, DateTimeOffset OccurredAt);
