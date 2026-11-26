using Platform.Modules.Comments.Contracts;

namespace Platform.Modules.Comments;

/// <summary>A shown comment that may be the best one under its post: what the choice needs, without the text.</summary>
/// <param name="Id">The comment's identifier.</param>
/// <param name="PostId">The post it is under.</param>
/// <param name="CreatedAt">When it was written.</param>
public sealed record BestCandidate(Guid Id, Guid PostId, DateTimeOffset CreatedAt);

/// <summary>
/// The choice of the best comment under a post for its feed card (E08-15, ADR-0062), with no database: the most liked
/// shown comment, the earlier one on a tie (and the smaller id at the same moment, so the choice is total).
/// </summary>
public static class BestComments
{
    /// <summary>
    /// The best of <paramref name="candidates"/> under each post, by the like counts <paramref name="likes"/> (a
    /// comment missing there has none). The candidates are every liked shown comment and the earliest shown one of each
    /// post: when no shown comment has a like, all are equal and the earliest wins, so the others need not be read.
    /// </summary>
    public static IReadOnlyDictionary<Guid, BestCandidate> Pick(
        IEnumerable<BestCandidate> candidates, IReadOnlyDictionary<Guid, int> likes)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(likes);
        return candidates
            .DistinctBy(c => c.Id)
            .GroupBy(c => c.PostId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => likes.GetValueOrDefault(c.Id))
                    .ThenBy(c => c.CreatedAt)
                    .ThenBy(c => c.Id)
                    .First());
    }

    /// <summary>
    /// The beginning of <paramref name="text"/> for a card: the whole text up to <see cref="BestComment.ExcerptLength"/>
    /// characters, otherwise that many, trimmed, with «…». A character outside the basic plane is never cut in two.
    /// </summary>
    public static string Excerpt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= BestComment.ExcerptLength)
        {
            return text;
        }

        var length = char.IsHighSurrogate(text[BestComment.ExcerptLength - 1])
            ? BestComment.ExcerptLength - 1
            : BestComment.ExcerptLength;
        return string.Concat(text.AsSpan(0, length).TrimEnd(), "…");
    }
}
