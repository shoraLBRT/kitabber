namespace Platform.Modules.Reactions;

/// <summary>What a reaction is on (FR-REACT).</summary>
public enum ReactionTarget
{
    Post = 1,
    Comment = 2,
}

/// <summary>
/// Which reaction it is (E10-02, ADR-0101): one kind per picture of the set (<see cref="ReactionGraphics.Codes"/>).
/// One person has one reaction on a target, changed or taken back (FR-REACT). The column keeps the name, so a new kind
/// needs no migration.
/// </summary>
public enum ReactionKind
{
    Like = 1,
    Heart = 2,
    Smile = 3,
    Clap = 4,
    Idea = 5,
    Wow = 6,
    Sad = 7,
}

/// <summary>The codes of the reaction kinds, as the client and the directory of reactions name them (ADR-0101).</summary>
public static class ReactionKinds
{
    private static readonly Dictionary<string, ReactionKind> ByCode = Enum.GetValues<ReactionKind>()
        .ToDictionary(CodeOf, k => k, StringComparer.Ordinal);

    /// <summary>The code of <paramref name="kind"/>: <c>like</c>, <c>heart</c>, …</summary>
    public static string CodeOf(ReactionKind kind) => kind switch
    {
        ReactionKind.Like => "like",
        ReactionKind.Heart => "heart",
        ReactionKind.Smile => "smile",
        ReactionKind.Clap => "clap",
        ReactionKind.Idea => "idea",
        ReactionKind.Wow => "wow",
        ReactionKind.Sad => "sad",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A reaction kind without a code"),
    };

    /// <summary>The kind of <paramref name="code"/>; <c>null</c> for a code the platform has no kind for.</summary>
    public static ReactionKind? Parse(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var kind) ? kind : null;
}

/// <summary>
/// One person's reaction on a post or a comment (FR-REACT, ADR-0061): one per person and target, so a repeated put
/// changes nothing and choosing another kind changes this row (ADR-0101). The target, its post and the person are known
/// only by their identifiers (FR-ID-1).
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

    public ReactionKind Kind { get; set; } = ReactionKind.Like;

    public DateTimeOffset CreatedAt { get; init; }

    public static Reaction Like(ReactionTarget target, Guid targetId, Guid postId, Guid accountId, DateTimeOffset now) =>
        Put(target, targetId, postId, accountId, ReactionKind.Like, now);

    public static Reaction Put(
        ReactionTarget target, Guid targetId, Guid postId, Guid accountId, ReactionKind kind, DateTimeOffset now)
    {
        if (targetId == Guid.Empty || postId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new ArgumentException("A reaction needs its target, its post and the account that puts it.");
        }

        if (target == ReactionTarget.Post && targetId != postId)
        {
            throw new ArgumentException("A reaction on a post is under that post.", nameof(postId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "An unknown reaction kind");
        }

        return new Reaction
        {
            TargetKind = target,
            TargetId = targetId,
            AccountId = accountId,
            PostId = postId,
            Kind = kind,
            CreatedAt = now,
        };
    }
}
