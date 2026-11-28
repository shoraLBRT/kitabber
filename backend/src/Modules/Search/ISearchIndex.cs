namespace Platform.Modules.Search;

/// <summary>The order of a search's results (E14-03, ADR-0124).</summary>
public enum SearchOrder
{
    /// <summary>The most relevant first.</summary>
    Relevance,

    /// <summary>The newest first, by <see cref="IndexedPost.PublishedAt"/>; for posts only.</summary>
    Date,
}

/// <summary>A page of a search on the results page (E14-03, ADR-0124).</summary>
/// <param name="Text">The query, normalized: every word of it must be in a document in some form.</param>
/// <param name="From">How many results come before the page.</param>
/// <param name="Size">How many results the page holds at most.</param>
/// <param name="Order">The order of the results.</param>
/// <param name="TitlesOnly">Posts: the query is matched against the title only.</param>
/// <param name="PublishedSince">Posts: only those published at this moment or later.</param>
/// <param name="BlogKind">Blogs: only <see cref="IndexedBlog.PersonalKind"/> or <see cref="IndexedBlog.TopicKind"/>.</param>
/// <param name="ExcludedAccounts">
/// The accounts the reader blocked (ADR-0091): their posts and their personal blogs are left out; topics never are.
/// </param>
public sealed record SearchQuery(
    string Text,
    int From,
    int Size,
    SearchOrder Order = SearchOrder.Relevance,
    bool TitlesOnly = false,
    DateTimeOffset? PublishedSince = null,
    string? BlogKind = null,
    IReadOnlyCollection<Guid>? ExcludedAccounts = null);

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

    /// <summary>
    /// Up to <paramref name="size"/> documents of <paramref name="index"/> for a query still being typed (E14-02,
    /// ADR-0097): every word but the last in some form, the last one as the start of a word, most relevant first.
    /// </summary>
    Task<IReadOnlyList<IndexedDocument>> SuggestAsync(
        SearchIndexKind index, string query, int size, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the documents of <paramref name="index"/> that have every word of <see cref="SearchQuery.Text"/> in
    /// some form, filtered and ordered as <paramref name="query"/> says (E14-03, ADR-0124).
    /// </summary>
    Task<IReadOnlyList<IndexedDocument>> FindAsync(
        SearchIndexKind index, SearchQuery query, CancellationToken cancellationToken);
}
