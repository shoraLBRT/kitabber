namespace Platform.Modules.Comments;

/// <summary>
/// When an account last opened the comments of a post (E09-08, ADR-0109): the comments written after it are new to
/// the account. One row per account and post, moved on by every opening; the account and the post are known only by
/// their identifiers (FR-ID-1).
/// </summary>
public sealed class CommentVisit
{
    public Guid AccountId { get; init; }

    public Guid PostId { get; init; }

    public DateTimeOffset VisitedAt { get; set; }
}
