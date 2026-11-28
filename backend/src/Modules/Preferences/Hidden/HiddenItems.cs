namespace Platform.Modules.Preferences.Hidden;

/// <summary>
/// A post a person hid from their feeds (E11-07, FR-SUB, ADR-0119): <see cref="OwnerAccountId"/> no longer sees
/// <see cref="PostId"/> in any feed. Both are only identifiers, with no foreign key to another module's schema.
/// </summary>
public sealed class HiddenPost
{
    public Guid OwnerAccountId { get; init; }

    public Guid PostId { get; init; }

    public DateTimeOffset HiddenAt { get; init; }
}

/// <summary>
/// A topic a person hid from their feeds (E11-07, FR-TOPIC, ADR-0119): no post of <see cref="BlogId"/> comes to
/// <see cref="OwnerAccountId"/>'s feeds, except the topic's own page.
/// </summary>
public sealed class HiddenTopic
{
    public Guid OwnerAccountId { get; init; }

    public Guid BlogId { get; init; }

    public DateTimeOffset HiddenAt { get; init; }
}

/// <summary>The rules of hiding from feeds, without a database (ADR-0119).</summary>
public static class HiddenRules
{
    /// <summary>
    /// The most posts, and separately the most topics, one person may hide (предложение, требует подтверждения): the
    /// same bound as the black list (ADR-0091), so the sets a reader's feeds filter by stay small.
    /// </summary>
    public const int MaxHidden = 1000;

    /// <summary>Why one more post may not be hidden, or <c>null</c> when it may.</summary>
    public static string? HidePostProblem(Guid postId, int alreadyHidden) =>
        postId == Guid.Empty ? "Не указан пост."
        : alreadyHidden >= MaxHidden ? $"Скрыть можно не больше {MaxHidden} постов."
        : null;

    /// <summary>Why one more topic may not be hidden, or <c>null</c> when it may.</summary>
    public static string? HideTopicProblem(Guid blogId, int alreadyHidden) =>
        blogId == Guid.Empty ? "Не указана тема."
        : alreadyHidden >= MaxHidden ? $"Скрыть можно не больше {MaxHidden} тем."
        : null;
}
