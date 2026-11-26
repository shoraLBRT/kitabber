namespace Platform.Modules.Posts.Contracts;

/// <summary>A published post as the search index holds it (ADR-0094).</summary>
/// <param name="Id">The post.</param>
/// <param name="BlogId">The blog (a personal blog or a topic) it is in.</param>
/// <param name="AuthorAccountId">Its author.</param>
/// <param name="Title">The title; <c>null</c> for a short post.</param>
/// <param name="Text">The text of its blocks without markup, a block per line.</param>
/// <param name="PublishedAt">When it was published.</param>
public sealed record PostSearchDocument(
    Guid Id, Guid BlogId, Guid AuthorAccountId, string? Title, string Text, DateTimeOffset PublishedAt);

/// <summary>
/// The posts the search index holds (ADR-0094): the Posts module implements it over its own tables. Only published
/// posts that are not deleted are there.
/// </summary>
public interface IPostSearchSource
{
    /// <summary>The post <paramref name="postId"/>, or <c>null</c> when readers do not see it: unknown, a draft or deleted.</summary>
    Task<PostSearchDocument?> FindAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>
    /// Up to <paramref name="size"/> posts in the order of their identifiers, after <paramref name="after"/> (from the
    /// start when <c>null</c>): the full reindex walks them page by page.
    /// </summary>
    Task<IReadOnlyList<PostSearchDocument>> PageAsync(Guid? after, int size, CancellationToken cancellationToken);
}
