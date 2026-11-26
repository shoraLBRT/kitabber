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
}
