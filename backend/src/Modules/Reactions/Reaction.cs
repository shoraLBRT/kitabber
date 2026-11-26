namespace Platform.Modules.Reactions;

/// <summary>What a reaction is on (FR-REACT).</summary>
public enum ReactionTarget
{
    Post = 1,
    Comment = 2,
}

/// <summary>
/// Which reaction it is. Only the like for now (E10-01); the set of reactions (E10-02) adds its kinds here, and one
/// person still has one reaction on a target, changed or taken back (FR-REACT).
/// </summary>
public enum ReactionKind
{
    Like = 1,
}

/// <summary>
/// One person's reaction on a post or a comment (FR-REACT, ADR-0061): one per person and target, so a repeated like
/// changes nothing. The target, its post and the person are known only by their identifiers (FR-ID-1).
/// </summary>
public sealed class Reaction
{
    public ReactionTarget TargetKind { get; init; }

    /// <summary>The post or the comment.</summary>
    public Guid TargetId { get; init; }

    /// <summary>Who reacted.</summary>
    public Guid AccountId { get; init; }

    /// <summary>The post the target is under (the post itself for a post): the page of a post reads all its likes at once.</summary>
    public Guid PostId { get; init; }

    public ReactionKind Kind { get; init; } = ReactionKind.Like;

    public DateTimeOffset CreatedAt { get; init; }

    public static Reaction Like(ReactionTarget target, Guid targetId, Guid postId, Guid accountId, DateTimeOffset now)
    {
        if (targetId == Guid.Empty || postId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new ArgumentException("A like needs its target, its post and the account that likes it.");
        }

        if (target == ReactionTarget.Post && targetId != postId)
        {
            throw new ArgumentException("A like on a post is under that post.", nameof(postId));
        }

        return new Reaction
        {
            TargetKind = target,
            TargetId = targetId,
            AccountId = accountId,
            PostId = postId,
            Kind = ReactionKind.Like,
            CreatedAt = now,
        };
    }
}
