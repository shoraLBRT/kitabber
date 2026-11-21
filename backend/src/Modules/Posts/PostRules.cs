using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Posts;

/// <summary>Who may do what with a post (FR-POST, ADR-0029), and how its fields are cleaned.</summary>
public static class PostRules
{
    /// <summary>The title trimmed; an empty or blank one becomes <c>null</c> (a short post, FR-EDITOR).</summary>
    public static string? CleanTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) ? null : title.Trim();

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
