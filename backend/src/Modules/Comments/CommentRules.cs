namespace Platform.Modules.Comments;

/// <summary>How a comment's text is cleaned and which replies are allowed (FR-COMMENT, FR-TG-6, ADR-0048).</summary>
public static class CommentRules
{
    /// <summary>The longest text of a comment, in characters, after trimming.</summary>
    public const int MaxTextLength = 5000;

    /// <summary>The longest name of a guest author.</summary>
    public const int MaxGuestNameLength = 128;

    /// <summary>The text trimmed; an empty or blank one becomes <c>null</c>.</summary>
    public static string? CleanText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// Why a native comment may not reply to <paramref name="parent"/>, or <c>null</c> when it may: no one replies to
    /// a deleted comment (ADR-0059) nor a guest one (FR-TG-6), and a reply deeper than <paramref name="maxDepth"/>
    /// levels is refused.
    /// </summary>
    public static string? ReplyProblem(Comment parent, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return parent.IsDeleted ? "Комментарий удалён: ответить на него нельзя."
            : parent.Kind == CommentKind.Guest ? "На гостевой комментарий ответить нельзя."
            : parent.Depth >= maxDepth ? $"Ветка не глубже {maxDepth} уровней: ответьте выше по ветке."
            : null;
    }

    /// <summary>
    /// Until when the author may change the text of <paramref name="comment"/> (E09-05, ADR-0059): its time plus
    /// <paramref name="window"/>; <c>null</c> when no one may — a guest comment, which has no author on the platform,
    /// and a deleted one.
    /// </summary>
    public static DateTimeOffset? EditableUntil(Comment comment, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return comment.Kind == CommentKind.Native && !comment.IsDeleted ? comment.CreatedAt + window : null;
    }

    /// <summary>
    /// Whether <paramref name="account"/> may change the text of <paramref name="comment"/> (<paramref name="edit"/>)
    /// or delete it (E09-05, ADR-0059): only the author does either, the text changes only before
    /// <see cref="EditableUntil"/>, and deleting has no time limit. A deleted comment is not there to change.
    /// </summary>
    public static CommentChange ChangeProblem(
        Comment comment, Guid account, bool edit, DateTimeOffset now, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return comment.IsDeleted ? CommentChange.NotFound
            : comment.Kind != CommentKind.Native || comment.AuthorAccountId != account ? CommentChange.NotAuthor
            : edit && now >= EditableUntil(comment, window) ? CommentChange.EditClosed
            : CommentChange.Allowed;
    }
}

/// <summary>Whether a change of a comment by an account is allowed (E09-05, ADR-0059).</summary>
public enum CommentChange
{
    /// <summary>The account may make the change.</summary>
    Allowed,

    /// <summary>The comment is deleted already: there is nothing to change.</summary>
    NotFound,

    /// <summary>Only the author changes or deletes a comment.</summary>
    NotAuthor,

    /// <summary>The time to change the text is over.</summary>
    EditClosed,
}
