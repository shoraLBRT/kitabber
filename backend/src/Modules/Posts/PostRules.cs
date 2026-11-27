using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Posts;

/// <summary>Who may do what with a post (FR-POST, ADR-0029), and how its fields are cleaned.</summary>
public static class PostRules
{
    /// <summary>The title trimmed; an empty or blank one becomes <c>null</c> (a short post, FR-EDITOR).</summary>
    public static string? CleanTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) ? null : title.Trim();

    /// <summary>The longest text over a repost, in characters (E10-08, ADR-0117).</summary>
    public const int RepostTextMaxLength = 1000;

    /// <summary>The text over a repost trimmed; an empty or blank one becomes <c>null</c> (a repost without a text).</summary>
    public static string? CleanRepostText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// Why <paramref name="accountId"/> may not repost <paramref name="original"/>, or <c>null</c> when they may
    /// (E10-08, ADR-0117): only a published post of someone else is reposted, with a text of at most
    /// <see cref="RepostTextMaxLength"/> characters. A repost is not reposted itself: the endpoint reposts its original.
    /// </summary>
    public static RepostRefusal? RepostRefusalOf(Post original, Guid accountId, string? text)
    {
        ArgumentNullException.ThrowIfNull(original);
        return original.IsDeleted || original.Status != PostStatus.Published || original.IsRepost
                ? RepostRefusal.NotFound
            : original.AuthorAccountId == accountId ? RepostRefusal.OwnPost
            : CleanRepostText(text)?.Length > RepostTextMaxLength ? RepostRefusal.TextTooLong
            : null;
    }

    /// <summary>
    /// Whether <paramref name="accountId"/> may put a post in <paramref name="blog"/>: a personal blog takes posts of
    /// its owner only, a topic takes posts of any signed-in account (the rules of a topic are E11, #186).
    /// </summary>
    public static bool MayPostIn(BlogSummary blog, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return blog.Kind == BlogKind.Topic || blog.OwnerAccountId == accountId;
    }

    /// <summary>Whether <paramref name="accountId"/> sees <paramref name="post"/>: a draft only its author, a published post everyone.</summary>
    public static bool MaySee(Post post, Guid? accountId)
    {
        ArgumentNullException.ThrowIfNull(post);
        return !post.IsDeleted && (post.Status == PostStatus.Published || post.AuthorAccountId == accountId);
    }

    /// <summary>Whether <paramref name="accountId"/> may edit or delete <paramref name="post"/>: only its author.</summary>
    public static bool MayChange(Post post, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(post);
        return !post.IsDeleted && post.AuthorAccountId == accountId;
    }
}

/// <summary>Why a repost is refused (E10-08, ADR-0117).</summary>
public enum RepostRefusal
{
    /// <summary>The post is not there to repost: deleted, a draft, or a repost itself.</summary>
    NotFound = 1,

    /// <summary>One's own post is not reposted.</summary>
    OwnPost = 2,

    /// <summary>The text is longer than <see cref="PostRules.RepostTextMaxLength"/>.</summary>
    TextTooLong = 3,
}
