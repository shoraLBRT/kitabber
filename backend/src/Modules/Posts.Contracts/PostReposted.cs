namespace Platform.Modules.Posts.Contracts;

/// <summary>
/// A person reposted a post into their blog (E10-08, ADR-0117): an internal event of the platform, published through the
/// outbox of the Posts module in the same transaction as the repost. It carries everyone it concerns, so the Notifications
/// module tells the original's author without asking the Posts module from the worker (ADR-0092).
/// </summary>
/// <param name="RepostId">The repost: a post in the reposter's personal blog.</param>
/// <param name="PostId">The original post.</param>
/// <param name="PostAuthorAccountId">The author of the original.</param>
/// <param name="ReposterAccountId">The person who reposted it.</param>
/// <param name="OccurredAt">When it was reposted.</param>
public sealed record PostReposted(
    Guid RepostId,
    Guid PostId,
    Guid PostAuthorAccountId,
    Guid ReposterAccountId,
    DateTimeOffset OccurredAt);
