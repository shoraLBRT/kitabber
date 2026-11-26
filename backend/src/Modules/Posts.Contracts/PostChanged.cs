namespace Platform.Modules.Posts.Contracts;

/// <summary>
/// What readers see of a post changed (ADR-0094): it was published, a published post was edited or it was deleted. An
/// internal event of the platform, published through the outbox of the Posts module in the same transaction as the
/// change. It carries no content: a consumer, such as the Search module, reads the post's current state through
/// <see cref="IPostSearchSource"/>, so the order and the repeats of the events do not matter.
/// </summary>
/// <param name="PostId">The post.</param>
/// <param name="OccurredAt">When it changed.</param>
public sealed record PostChanged(Guid PostId, DateTimeOffset OccurredAt);
