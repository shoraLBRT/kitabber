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
    /// a guest comment (FR-TG-6), and a reply deeper than <paramref name="maxDepth"/> levels is refused.
    /// </summary>
    public static string? ReplyProblem(Comment parent, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return parent.Kind == CommentKind.Guest ? "На гостевой комментарий ответить нельзя."
            : parent.Depth >= maxDepth ? $"Ветка не глубже {maxDepth} уровней: ответьте выше по ветке."
            : null;
    }
}
