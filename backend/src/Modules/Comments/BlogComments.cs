using Platform.Http.Pagination;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Comments;

/// <summary>The position of a page of the tab «Комментарии» (ADR-0006): the keyset of the last comment shown.</summary>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="Id">Its identifier, for comments written at the same moment.</param>
public sealed record BlogCommentCursor(DateTimeOffset CreatedAt, Guid Id);

/// <summary>One comment of the tab «Комментарии» of a blog or a topic (E09-15, FR-COMMENT, ADR-0114).</summary>
/// <param name="Id">The comment's identifier: the link leads to it on its post's page (ADR-0107).</param>
/// <param name="PostId">The published post it is under.</param>
/// <param name="PostBlogId">The blog of the post: the address by it leads to the canonical one (ADR-0041).</param>
/// <param name="PostTitle">The title of the post; <c>null</c> for a post without one.</param>
/// <param name="PostSlug">The slug of the post's address; <c>null</c> when its title gives none.</param>
/// <param name="Kind"><c>native</c> (an account of the platform) or <c>guest</c> (an outside author, FR-TG-6).</param>
/// <param name="AuthorAccountId">The author of a native comment; <c>null</c> for a guest one.</param>
/// <param name="Guest">The author of a guest comment; <c>null</c> for a native one.</param>
/// <param name="Text">The whole text.</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="EditedAt">When its author last changed the text («изменено»); <c>null</c> for a text never changed.</param>
public sealed record BlogCommentView(
    Guid Id,
    Guid PostId,
    Guid PostBlogId,
    string? PostTitle,
    string? PostSlug,
    string Kind,
    Guid? AuthorAccountId,
    GuestAuthorView? Guest,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt);

/// <summary>
/// The rules of the tab «Комментарии» (E09-15, ADR-0114), with no database: which of the comments read from the
/// database the reader sees, and where the next page starts. A deleted comment is never read; a comment under a post
/// that is no longer published and one by an author the reader blocked (ADR-0091) are left out here.
/// </summary>
public static class BlogComments
{
    /// <summary>
    /// How many batches one read looks through at most: a page whose comments are mostly left out stops there, shorter,
    /// with the cursor of the last comment looked at, so one read never walks the whole history.
    /// </summary>
    public const int MaxBatches = 5;

    /// <summary>
    /// The comments of <paramref name="batch"/> the reader sees, in their order: under a published post (one of
    /// <paramref name="published"/>), not deleted, and not by an account in <paramref name="blocked"/>.
    /// </summary>
    public static IEnumerable<BlogCommentView> Shown(
        IEnumerable<Comment> batch, IReadOnlyDictionary<Guid, PostHeadline> published, IReadOnlySet<Guid> blocked)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(published);
        ArgumentNullException.ThrowIfNull(blocked);
        foreach (var comment in batch)
        {
            if (comment.IsDeleted
                || !published.TryGetValue(comment.PostId, out var post)
                || (comment.AuthorAccountId is { } author && blocked.Contains(author)))
            {
                continue;
            }

            yield return View(comment, post);
        }
    }

    /// <summary>
    /// The page of <paramref name="kept"/>: the first <paramref name="limit"/> and the cursor after the last of them
    /// when there are more; when the read stopped before the comments ran out (<paramref name="exhausted"/> is
    /// <c>false</c>), the cursor after <paramref name="lastSeen"/>, the last comment looked at, so the next page goes on
    /// from there; no cursor when nothing is left.
    /// </summary>
    public static CursorPage<BlogCommentView> Page(
        IReadOnlyList<BlogCommentView> kept, int limit, BlogCommentCursor? lastSeen, bool exhausted)
    {
        ArgumentNullException.ThrowIfNull(kept);
        if (kept.Count > limit)
        {
            var items = kept.Take(limit).ToArray();
            return new CursorPage<BlogCommentView>(
                items, CursorCodec.Encode(new BlogCommentCursor(items[^1].CreatedAt, items[^1].Id)));
        }

        return new CursorPage<BlogCommentView>(
            kept, !exhausted && lastSeen is not null ? CursorCodec.Encode(lastSeen) : null);
    }

    /// <summary>The view of a shown <paramref name="comment"/> under the published <paramref name="post"/>.</summary>
    internal static BlogCommentView View(Comment comment, PostHeadline post) => new(
        comment.Id,
        post.Id,
        post.BlogId,
        post.Title,
        post.Slug,
        comment.Kind == CommentKind.Guest ? "guest" : "native",
        comment.AuthorAccountId,
        comment.Guest is { } guest ? new GuestAuthorView(guest.Name, guest.Url?.AbsoluteUri) : null,
        comment.Text,
        comment.CreatedAt,
        comment.EditedAt);
}
