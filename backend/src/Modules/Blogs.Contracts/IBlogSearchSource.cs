namespace Platform.Modules.Blogs.Contracts;

/// <summary>A personal blog or a topic as the search index holds it (ADR-0094).</summary>
/// <param name="Id">The blog.</param>
/// <param name="Kind">A personal blog or a topic.</param>
/// <param name="OwnerAccountId">Its owner; <c>null</c> for a topic without one.</param>
/// <param name="Name">The name, when it has one.</param>
/// <param name="Nickname">The nickname of its address, when it has one.</param>
/// <param name="Description">The description, when it has one.</param>
public sealed record BlogSearchDocument(
    Guid Id, BlogKind Kind, Guid? OwnerAccountId, string? Name, string? Nickname, string? Description);

/// <summary>
/// The blogs and topics the search index holds (ADR-0094): the Blogs module implements it over its own tables. A blog
/// with neither a name nor a nickname cannot be found by anything and is not there.
/// </summary>
public interface IBlogSearchSource
{
    /// <summary>The blog <paramref name="blogId"/>, or <c>null</c> when it is unknown or has nothing to be found by.</summary>
    Task<BlogSearchDocument?> FindAsync(Guid blogId, CancellationToken cancellationToken);

    /// <summary>
    /// Up to <paramref name="size"/> blogs in the order of their identifiers, after <paramref name="after"/> (from the
    /// start when <c>null</c>): the full reindex walks them page by page.
    /// </summary>
    Task<IReadOnlyList<BlogSearchDocument>> PageAsync(Guid? after, int size, CancellationToken cancellationToken);
}
