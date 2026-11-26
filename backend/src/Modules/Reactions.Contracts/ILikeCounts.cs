namespace Platform.Modules.Reactions.Contracts;

/// <summary>
/// The like counts other modules may ask about (ADR-0062): the Reactions module implements it over its own table, so
/// the caller never reads the schema <c>reactions</c>.
/// </summary>
public interface ILikeCounts
{
    /// <summary>
    /// The number of likes of every comment under the posts <paramref name="postIds"/> that has any, by the comment's
    /// id, read with one query. A comment with no likes is not in the result; whether a comment is still shown is the
    /// caller's to check.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CommentLikesAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken);
}
