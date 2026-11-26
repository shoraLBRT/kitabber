namespace Platform.Modules.Comments.Contracts;

/// <summary>
/// A person wrote a comment (ADR-0092): an internal event of the platform, published through the outbox of the Comments
/// module in the same transaction as the comment. It carries the people the comment concerns, so that a consumer, such
/// as the Notifications module, needs to ask no other module. A guest comment carried over by the bridge is not
/// announced: it has no account to act.
/// </summary>
/// <param name="CommentId">The new comment.</param>
/// <param name="PostId">The post it is under.</param>
/// <param name="PostAuthorAccountId">The author of the post.</param>
/// <param name="AuthorAccountId">The author of the comment.</param>
/// <param name="ParentCommentId">The comment it replies to; <c>null</c> for a reply to the post.</param>
/// <param name="ParentAuthorAccountId">
/// The author of <paramref name="ParentCommentId"/>; <c>null</c> for a reply to the post or to a guest comment.
/// </param>
/// <param name="MentionedAccountIds">
/// The accounts the text mentions by <c>@ник</c>; empty until mentions in comments come (E09-11).
/// </param>
/// <param name="OccurredAt">When the comment was written.</param>
public sealed record CommentPosted(
    Guid CommentId,
    Guid PostId,
    Guid PostAuthorAccountId,
    Guid AuthorAccountId,
    Guid? ParentCommentId,
    Guid? ParentAuthorAccountId,
    IReadOnlyList<Guid> MentionedAccountIds,
    DateTimeOffset OccurredAt);
