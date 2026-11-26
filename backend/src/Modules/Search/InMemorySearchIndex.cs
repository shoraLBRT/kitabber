using System.Collections.Concurrent;

namespace Platform.Modules.Search;

/// <summary>
/// The fake search engine of the tests (<c>Search:Provider=InMemory</c>): documents live in the process. A query matches
/// a document that contains every word of it, ignoring case; there is no morphology, which only
/// <see cref="OpenSearchIndex"/> has.
/// </summary>
public sealed class InMemorySearchIndex : ISearchIndex
{
    private readonly ConcurrentDictionary<SearchIndexKind, ConcurrentDictionary<Guid, IndexedDocument>> _indexes = new();

    /// <summary>What <paramref name="index"/> holds now.</summary>
    public IReadOnlyCollection<IndexedDocument> Documents(SearchIndexKind index) => [.. Of(index).Values];

    public Task PutAsync(IndexedDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        Of(document.Index)[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(SearchIndexKind index, Guid id, CancellationToken cancellationToken)
    {
        Of(index).TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public async Task<int> ReplaceAllAsync(
        SearchIndexKind index, IAsyncEnumerable<IndexedDocument> documents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var fresh = new ConcurrentDictionary<Guid, IndexedDocument>();
        await foreach (var document in documents.WithCancellation(cancellationToken))
        {
            fresh[document.Id] = document;
        }

        _indexes[index] = fresh;
        return fresh.Count;
    }

    public Task<IReadOnlyList<Guid>> SearchAsync(
        SearchIndexKind index, string query, int size, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<Guid> found = [.. Matching(index, query).Select(d => d.Id).Take(size)];
        return Task.FromResult(found);
    }

    /// <summary>The same words as <see cref="SearchAsync"/>, a part of a word being enough; in the order of the ids.</summary>
    public Task<IReadOnlyList<IndexedDocument>> SuggestAsync(
        SearchIndexKind index, string query, int size, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<IndexedDocument> found = [.. Matching(index, query).OrderBy(d => d.Id).Take(size)];
        return Task.FromResult(found);
    }

    private IEnumerable<IndexedDocument> Matching(SearchIndexKind index, string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? []
            : Of(index).Values.Where(d => words.All(w => d.SearchableText()
                .Any(t => t?.Contains(w, StringComparison.OrdinalIgnoreCase) == true)));
    }

    private ConcurrentDictionary<Guid, IndexedDocument> Of(SearchIndexKind index) => _indexes.GetOrAdd(index, _ => new());
}
