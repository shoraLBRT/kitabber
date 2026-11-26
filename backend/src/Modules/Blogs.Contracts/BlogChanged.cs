namespace Platform.Modules.Blogs.Contracts;

/// <summary>
/// What readers find a blog or a topic by changed (ADR-0094): its name, nickname or description, or a topic was created.
/// An internal event of the platform, published through the outbox of the Blogs module in the same transaction as the
/// change. It carries no content: a consumer, such as the Search module, reads the blog's current state through
/// <see cref="IBlogSearchSource"/>, so the order and the repeats of the events do not matter.
/// </summary>
/// <param name="BlogId">The personal blog or the topic.</param>
/// <param name="OccurredAt">When it changed.</param>
public sealed record BlogChanged(Guid BlogId, DateTimeOffset OccurredAt);
