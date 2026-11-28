namespace Platform.Modules.Blogs.Contracts;

/// <summary>
/// The subscriptions other modules may ask about (E11-03, ADR-0085), such as «Моя лента» (E08-04): the Blogs module
/// answers over its own tables, so the caller never reads the schema <c>blogs</c>.
/// </summary>
public interface IBlogSubscriptions
{
    /// <summary>
    /// The blogs and topics <paramref name="accountId"/> is subscribed to, newest subscription first; a personal blog
    /// carries its owner, whose posts are the blog's posts. Empty for an account with no subscriptions.
    /// </summary>
    Task<IReadOnlyList<BlogSummary>> SubscribedBlogsAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="accountId"/> is subscribed to the blog <paramref name="blogId"/>.</summary>
    Task<bool> IsSubscribedAsync(Guid accountId, Guid blogId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of <paramref name="blogIds"/> <paramref name="accountId"/> is subscribed to, read at once for a page of
    /// rows with «Подписаться», such as the search results (E14-03).
    /// </summary>
    Task<IReadOnlySet<Guid>> SubscribedAmongAsync(
        Guid accountId, IReadOnlyCollection<Guid> blogIds, CancellationToken cancellationToken);

    /// <summary>
    /// Who wants to know about a new post (E11-04, ADR-0118): the subscribers with notifications on of the blog or the
    /// topic <paramref name="blogId"/> the post is in and of the personal blog of its author
    /// <paramref name="authorAccountId"/>, each once, without the author. Empty when no one turned notifications on.
    /// </summary>
    Task<IReadOnlyList<Guid>> NewPostRecipientsAsync(Guid blogId, Guid authorAccountId, CancellationToken cancellationToken);
}
