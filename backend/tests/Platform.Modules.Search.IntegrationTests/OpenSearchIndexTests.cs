using Platform.Testing.Containers;

namespace Platform.Modules.Search.IntegrationTests;

/// <summary>
/// E14-01, ADR-0094: <see cref="OpenSearchIndex"/> on the OpenSearch of compose.yaml — Russian morphology, removal and
/// the full replacement of an index behind its alias. Every test has indexes of its own (a prefix).
/// </summary>
public sealed class OpenSearchIndexTests(OpenSearchFixture openSearch)
{
    private static readonly DateTimeOffset _at = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostIsFoundByAnotherFormOfItsWordsAndLeavesWhenRemoved()
    {
        using var index = NewIndex();
        var post = Post("Как воспитывать котов", "Коты любят спать на подоконниках.");
        var other = Post("Про собак", "Собаки гуляют.");

        await index.PutAsync(post, Ct);
        await index.PutAsync(other, Ct);

        await WaitForAsync(index, SearchIndexKind.Posts, "кот", [post.Id]);
        await WaitForAsync(index, SearchIndexKind.Posts, "коты на подоконнике", [post.Id]);
        await index.RemoveAsync(SearchIndexKind.Posts, post.Id, Ct);
        await WaitForAsync(index, SearchIndexKind.Posts, "кот", []);
        // Removing what is not there is not an error.
        await index.RemoveAsync(SearchIndexKind.Posts, post.Id, Ct);
    }

    [Fact]
    public async Task BlogsAndTopicsAreFoundByNameNicknameAndDescription()
    {
        using var index = NewIndex();
        var blog = new IndexedBlog(Guid.CreateVersion7(_at), IndexedBlog.PersonalKind, Guid.NewGuid(), "Анна", "anna_cats",
            "Пишу о кошках");
        var topic = new IndexedBlog(Guid.CreateVersion7(_at), IndexedBlog.TopicKind, null, "Астрономия", "astro", null);

        await index.PutAsync(blog, Ct);
        await index.PutAsync(topic, Ct);

        await WaitForAsync(index, SearchIndexKind.Blogs, "кошка", [blog.Id]);
        await WaitForAsync(index, SearchIndexKind.Blogs, "anna_cats", [blog.Id]);
        await WaitForAsync(index, SearchIndexKind.Blogs, "астрономии", [topic.Id]);
    }

    [Fact]
    public async Task ReplacingAnIndexKeepsOnlyTheNewDocumentsAndCanBeRepeated()
    {
        using var index = NewIndex();
        var stale = Post("Старый пост", "Удалён давно");
        await index.PutAsync(stale, Ct);
        var fresh = Enumerable.Range(0, OpenSearchIndex.BulkSize + 3)
            .Select(i => Post($"Новый пост {i}", "Свежий текст"))
            .ToList();

        Assert.Equal(fresh.Count, await index.ReplaceAllAsync(SearchIndexKind.Posts, Async(fresh), Ct));
        Assert.Equal(fresh.Count, await index.ReplaceAllAsync(SearchIndexKind.Posts, Async(fresh), Ct));

        Assert.Empty(await index.SearchAsync(SearchIndexKind.Posts, "старый", 10, Ct));
        Assert.Equal(10, (await index.SearchAsync(SearchIndexKind.Posts, "свежий", 10, Ct)).Count);
        // Writes after the replacement go into the new index.
        var later = Post("Поздний пост", "Пришёл после");
        await index.PutAsync(later, Ct);
        await WaitForAsync(index, SearchIndexKind.Posts, "поздний", [later.Id]);
    }

    [Fact]
    public async Task SearchingAnIndexThatWasNeverWrittenFindsNothing()
    {
        using var index = NewIndex();

        Assert.Empty(await index.SearchAsync(SearchIndexKind.Blogs, "что-нибудь", 10, Ct));
    }

    private OpenSearchIndex NewIndex() =>
        new(new OpenSearchOptions(openSearch.Url, $"t{Guid.NewGuid():N}_"), TimeProvider.System);

    private static IndexedPost Post(string title, string text) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), title, text, _at);

    private static async IAsyncEnumerable<IndexedDocument> Async(IEnumerable<IndexedDocument> documents)
    {
        foreach (var document in documents)
        {
            await Task.Yield();
            yield return document;
        }
    }

    // A write is searchable after the next refresh of the index (a second by default).
    private static async Task WaitForAsync(
        OpenSearchIndex index, SearchIndexKind kind, string query, IReadOnlyList<Guid> expected)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (true)
        {
            var found = await index.SearchAsync(kind, query, 10, Ct);
            if (found.Order().SequenceEqual(expected.Order()))
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, $"«{query}» found [{string.Join(", ", found)}]");
            await Task.Delay(200, Ct);
        }
    }
}
