namespace Platform.Modules.Comments.Contracts;

/// <summary>A comment as other modules see it.</summary>
/// <param name="Id">The comment's identifier.</param>
/// <param name="PostId">The post it is under.</param>
/// <param name="AuthorAccountId">The author of a native comment; <c>null</c> for a guest comment (FR-TG-6).</param>
public sealed record CommentSummary(Guid Id, Guid PostId, Guid? AuthorAccountId)
{
    /// <summary>An outside author's comment, carried over by the bridge: it has no account on the platform.</summary>
    public bool IsGuest => AuthorAccountId is null;
}

/// <summary>The best comment under a post, shown under the post's card in a feed (E08-15, ADR-0062).</summary>
/// <param name="Id">The comment's identifier.</param>
/// <param name="PostId">The post it is under.</param>
/// <param name="AuthorAccountId">The author of a native comment; <c>null</c> for a guest comment (FR-TG-6).</param>
/// <param name="GuestName">The name of a guest comment's outside author; <c>null</c> for a native comment.</param>
/// <param name="Excerpt">
/// The beginning of the text: the whole text when it is short, otherwise its first
/// <see cref="BestComment.ExcerptLength"/> characters and «…». The card cuts it further to a few lines.
/// </param>
/// <param name="Likes">The number of likes, the rating it was chosen by.</param>
/// <param name="CreatedAt">When it was written; the earlier of two equally rated comments is the best.</param>
public sealed record BestComment(
    Guid Id,
    Guid PostId,
    Guid? AuthorAccountId,
    string? GuestName,
    string Excerpt,
    int Likes,
    DateTimeOffset CreatedAt)
{
    /// <summary>How many characters of the text a feed card gets at most: more than its few lines ever show.</summary>
    public const int ExcerptLength = 300;
}

/// <summary>
/// The comments other modules may ask about (ADR-0061): the Comments module implements it over its own tables, so the
/// caller never reads the schema <c>comments</c>.
/// </summary>
public interface ICommentDirectory
{
    /// <summary>
    /// The comment <paramref name="commentId"/> under a published post, or <c>null</c> when there is none: unknown,
    /// deleted (a stub has no author and no text), or under a post that is a draft or deleted.
    /// </summary>
    Task<CommentSummary?> FindShownAsync(Guid commentId, CancellationToken cancellationToken);

    /// <summary>
    /// The best shown comment under each of the posts <paramref name="postIds"/> (E08-15, ADR-0062): the most liked,
    /// the earlier one on a tie; a deleted comment is never chosen. A post with no shown comment is not in the result.
    /// A whole page of a feed is read with a fixed number of queries, whatever its size. The caller gives posts it
    /// already knows are published.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, BestComment>> BestShownAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken);

    /// <summary>
    /// The number of shown comments under each of the posts <paramref name="postIds"/>, by the post's id, read with one
    /// query (E08-05, ADR-0086): every comment that is not deleted, replies and guest comments among them. A post with
    /// no shown comment is not in the result. The caller gives posts it already knows are published.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> ShownCountsAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken);
}
