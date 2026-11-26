namespace Platform.Modules.Reactions.Contracts;

/// <summary>
/// A person put a reaction on a post or a comment (ADR-0092): an internal event of the platform, published through the
/// outbox of the Reactions module in the same transaction as the reaction. A repeated put of a reaction already there
/// changes nothing and is not announced; taking a reaction back is not announced either.
/// </summary>
/// <param name="Target"><c>post</c> or <c>comment</c>.</param>
/// <param name="TargetId">The post or the comment.</param>
/// <param name="PostId">The post itself, or the post the comment is under.</param>
/// <param name="TargetAuthorAccountId">The author of the post or of the comment.</param>
/// <param name="AccountId">The person who put the reaction.</param>
/// <param name="Kind">The reaction, as the client names it: <c>like</c>.</param>
/// <param name="OccurredAt">When the reaction was put.</param>
public sealed record ReactionPut(
    string Target,
    Guid TargetId,
    Guid PostId,
    Guid TargetAuthorAccountId,
    Guid AccountId,
    string Kind,
    DateTimeOffset OccurredAt);
