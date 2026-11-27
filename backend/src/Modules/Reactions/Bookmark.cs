namespace Platform.Modules.Reactions;

/// <summary>
/// One person's bookmark on a post or a comment (E10-07, FR-REACT, ADR-0113): one per person and target, so a repeated
/// put changes nothing. The row keeps the author of the target, known when it is put, so the author's count needs no
/// other module; a guest comment has no author account. The target, its post and the people are known only by their
/// identifiers (FR-ID-1).
/// </summary>
public sealed class Bookmark
{
    public ReactionTarget TargetKind { get; init; }

    /// <summary>The post or the comment.</summary>
    public Guid TargetId { get; init; }

    /// <summary>Whose bookmark it is.</summary>
    public Guid AccountId { get; init; }

    /// <summary>The post the target is under (the post itself for a post).</summary>
    public Guid PostId { get; init; }

    /// <summary>The author of the target; <c>null</c> for a guest comment. Only the author sees the count.</summary>
    public Guid? TargetAuthorId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public static Bookmark Put(
        ReactionTarget target, Guid targetId, Guid postId, Guid accountId, Guid? targetAuthorId, DateTimeOffset now)
    {
        if (targetId == Guid.Empty || postId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new ArgumentException("A bookmark needs its target, its post and the account that puts it.");
        }

        if (target == ReactionTarget.Post && targetId != postId)
        {
            throw new ArgumentException("A bookmark on a post is under that post.", nameof(postId));
        }

        if (target == ReactionTarget.Post && targetAuthorId is null)
        {
            throw new ArgumentException("A post always has an author.", nameof(targetAuthorId));
        }

        return new Bookmark
        {
            TargetKind = target,
            TargetId = targetId,
            AccountId = accountId,
            PostId = postId,
            TargetAuthorId = targetAuthorId == Guid.Empty ? null : targetAuthorId,
            CreatedAt = now,
        };
    }
}
