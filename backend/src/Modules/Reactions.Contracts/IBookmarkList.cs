namespace Platform.Modules.Reactions.Contracts;

/// <summary>What a person keeps in their bookmarks (E04-12, ADR-0116): posts or comments.</summary>
public enum BookmarkKind
{
    Post,
    Comment,
}

/// <summary>
/// The position of a page of the section «Закладки» (ADR-0006, ADR-0116): the keyset of the last bookmark shown.
/// </summary>
/// <param name="BookmarkedAt">When it was put.</param>
/// <param name="TargetId">Its post or comment, for bookmarks put at the same moment.</param>
public sealed record BookmarkPosition(DateTimeOffset BookmarkedAt, Guid TargetId);

/// <summary>One bookmark of a person (E04-12, ADR-0116).</summary>
/// <param name="TargetId">The post or the comment.</param>
/// <param name="PostId">The post the target is under (the post itself for a post).</param>
/// <param name="BookmarkedAt">When the person put it.</param>
public sealed record BookmarkEntry(Guid TargetId, Guid PostId, DateTimeOffset BookmarkedAt)
{
    /// <summary>Where the page after this bookmark starts.</summary>
    public BookmarkPosition Position => new(BookmarkedAt, TargetId);
}

/// <summary>
/// The bookmarks of one person other modules may list (E04-12, ADR-0116): the Reactions module implements it over its
/// own table, so the Posts and Comments modules build the section «Закладки» without reading the schema
/// <c>reactions</c>. A bookmark stays when its target is deleted; whether the target is still shown is the caller's.
/// </summary>
public interface IBookmarkList
{
    /// <summary>
    /// At most <paramref name="count"/> bookmarks of the kind <paramref name="kind"/> that <paramref name="accountId"/>
    /// put, newest first, strictly after <paramref name="after"/> when it is given; read with one query.
    /// </summary>
    Task<IReadOnlyList<BookmarkEntry>> PageAsync(
        Guid accountId, BookmarkKind kind, BookmarkPosition? after, int count, CancellationToken cancellationToken);
}
