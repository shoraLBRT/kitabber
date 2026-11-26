using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Search;

/// <summary>How many documents a full reindex wrote.</summary>
public sealed record SearchReindexResult(int Posts, int Blogs);

/// <summary>
/// The full reindex (ADR-0094): every published post and every blog and topic with something to be found by is read
/// page by page from its owner and written into a new index, which then replaces the old one. It fills the indexes after
/// the first deployment, after the demo data and after a change of the mappings.
/// </summary>
public sealed class SearchReindexer(IPostSearchSource posts, IBlogSearchSource blogs, ISearchIndex index)
{
    /// <summary>The documents read from a source at once.</summary>
    public const int PageSize = 500;

    public async Task<SearchReindexResult> RunAsync(CancellationToken cancellationToken)
    {
        var postCount = await index.ReplaceAllAsync(
            SearchIndexKind.Posts,
            Walk((after, ct) => posts.PageAsync(after, PageSize, ct), p => p.Id, IndexedPost.Of, cancellationToken),
            cancellationToken);
        var blogCount = await index.ReplaceAllAsync(
            SearchIndexKind.Blogs,
            Walk((after, ct) => blogs.PageAsync(after, PageSize, ct), b => b.Id, IndexedBlog.Of, cancellationToken),
            cancellationToken);
        return new(postCount, blogCount);
    }

    private static async IAsyncEnumerable<IndexedDocument> Walk<T>(
        Func<Guid?, CancellationToken, Task<IReadOnlyList<T>>> page,
        Func<T, Guid> id,
        Func<T, IndexedDocument> document,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Guid? after = null;
        while (true)
        {
            var items = await page(after, cancellationToken);
            foreach (var item in items)
            {
                yield return document(item);
            }

            if (items.Count < PageSize)
            {
                yield break;
            }

            after = id(items[^1]);
        }
    }
}

/// <summary>
/// The command <c>search-reindex</c> of the workers host (ADR-0094):
/// <c>dotnet run --project backend/src/Workers -- search-reindex</c>, on a stand
/// <c>docker compose run --rm workers search-reindex</c>. It runs <see cref="SearchReindexer"/> and exits.
/// </summary>
public static class SearchReindexCommand
{
    public const string Name = "search-reindex";

    /// <returns>0 when done, 1 when it failed.</returns>
    public static async Task<int> RunAsync(
        IServiceProvider services, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            await using var scope = services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<SearchReindexer>().RunAsync(cancellationToken);
            await output.WriteLineAsync($"{Name}: {result.Posts} posts, {result.Blogs} blogs and topics");
            return 0;
        }
#pragma warning disable CA1031 // The command reports any failure as a non-zero exit code.
        catch (Exception e)
#pragma warning restore CA1031
        {
            await error.WriteLineAsync($"{Name} failed: {e.Message}");
            return 1;
        }
    }
}
