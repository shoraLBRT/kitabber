namespace Platform.Modules.Reactions.Contracts;

/// <summary>
/// The ratings of posts and comments other modules may order by (E10-05, ADR-0081): the sum of their reactions by the
/// weights of the Reactions module, kept up to date by it. The caller never reads the schema <c>reactions</c>.
/// </summary>
public interface IReactionRatings
{
    /// <summary>
    /// The rating of every comment under the posts <paramref name="postIds"/> that has one, by the comment's id, read
    /// with one query. A comment missing in the result has a rating of zero; whether it is still shown is the caller's
    /// to check.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CommentRatingsAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken);

    /// <summary>
    /// The rating of every post of <paramref name="postIds"/> that has one, by the post's id, read with one query; a
    /// post missing in the result has a rating of zero.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> PostRatingsAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken);
}
