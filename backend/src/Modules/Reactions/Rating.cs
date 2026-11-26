namespace Platform.Modules.Reactions;

/// <summary>
/// The rating of a post or a comment (E10-05, FR-REACT, ADR-0081): the sum of its reactions, each by the weight of its
/// kind in <see cref="ReactionWeights"/>. It is kept, not counted on every read, so that orders by rating stay one
/// indexed read; the module counts it again whenever a reaction on the target is put or taken back. A target whose
/// rating is zero has no row.
/// </summary>
public sealed class Rating
{
    public ReactionTarget TargetKind { get; init; }

    /// <summary>The post or the comment.</summary>
    public Guid TargetId { get; init; }

    /// <summary>The post the target is under (the post itself for a post): a post's comments are read at once.</summary>
    public Guid PostId { get; init; }

    public int Value { get; set; }
}

/// <summary>
/// The reference of reaction weights (E10-05, ADR-0081): how much one reaction of each kind adds to the rating of its
/// target. It lives in code, in this one place, so a weight changes through review together with the recount of the
/// kept ratings. Weights are not negative: a reaction never lowers a rating.
/// </summary>
public static class ReactionWeights
{
    /// <summary>The weight of one reaction of <paramref name="kind"/>; a kind without a weight is a programming error.</summary>
    public static int Of(ReactionKind kind) => kind switch
    {
        ReactionKind.Like => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A reaction kind without a weight"),
    };

    /// <summary>The rating of a target with <paramref name="counts"/> reactions of each kind.</summary>
    public static int RatingOf(IEnumerable<KeyValuePair<ReactionKind, int>> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var rating = 0;
        foreach (var (kind, count) in counts)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(counts));
            rating = checked(rating + (Of(kind) * count));
        }

        return rating;
    }
}
