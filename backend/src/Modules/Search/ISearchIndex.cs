namespace Platform.Modules.Search;

/// <summary>
/// The search engine behind the Search module (ADR-0094): OpenSearch (<see cref="OpenSearchIndex"/>) or the fake of the
/// tests (<see cref="InMemorySearchIndex"/>), by <c>Search:Provider</c>. Each index is created on its first write.
/// </summary>
public interface ISearchIndex
{
    /// <summary>Writes <paramref name="document"/> into its index, replacing the one with the same id.</summary>
    Task PutAsync(IndexedDocument document, CancellationToken cancellationToken);

    /// <summary>Removes the document <paramref name="id"/>; a missing one is not an error.</summary>
    Task RemoveAsync(SearchIndexKind index, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the whole <paramref name="index"/> with <paramref name="documents"/>: they are written into a new index,
    /// which then takes the place of the old one at once, so searches never see it half full. The number written.
    /// </summary>
    Task<int> ReplaceAllAsync(
        SearchIndexKind index, IAsyncEnumerable<IndexedDocument> documents, CancellationToken cancellationToken);

    /// <summary>
    /// The identifiers of up to <paramref name="size"/> documents of <paramref name="index"/> that have every word of
    /// <paramref name="query"/> in some form, most relevant first. The search API of E14-02 and E14-03 builds on it.
    /// </summary>
    Task<IReadOnlyList<Guid>> SearchAsync(
        SearchIndexKind index, string query, int size, CancellationToken cancellationToken);
}
