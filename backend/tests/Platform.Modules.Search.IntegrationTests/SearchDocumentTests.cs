using System.Text.Json.Nodes;

namespace Platform.Modules.Search.IntegrationTests;

/// <summary>
/// E14-01, ADR-0094: the JSON written to OpenSearch has exactly the fields of the strict mappings — the id is the key and
/// the index is where it goes, neither is a field.
/// </summary>
public sealed class SearchDocumentTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PostHasOnlyItsMappedFields()
    {
        var json = JsonNode.Parse(OpenSearchIndex.Serialize(
            new IndexedPost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Заголовок", "Текст", _at)))!.AsObject();

        Assert.Equal(["authorAccountId", "blogId", "publishedAt", "text", "title"], json.Select(p => p.Key).Order());
    }

    [Fact]
    public void BlogHasOnlyItsMappedFields()
    {
        var json = JsonNode.Parse(OpenSearchIndex.Serialize(
            new IndexedBlog(Guid.NewGuid(), IndexedBlog.TopicKind, null, "Тема", "topic", null)))!.AsObject();

        Assert.Equal(["description", "kind", "name", "nickname", "ownerAccountId"], json.Select(p => p.Key).Order());
    }

    [Fact]
    public void DocumentsReadBackAsTheyWereWritten()
    {
        IndexedDocument[] documents =
        [
            new IndexedPost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Короткий пост", _at),
            new IndexedBlog(Guid.NewGuid(), IndexedBlog.PersonalKind, Guid.NewGuid(), "Анна", "anna", "О кошках"),
            new IndexedBlog(Guid.NewGuid(), IndexedBlog.TopicKind, null, "Тема", null, null),
        ];

        foreach (var document in documents)
        {
            var source = JsonNode.Parse(OpenSearchIndex.Serialize(document))!.AsObject();
            Assert.Equal(document, OpenSearchIndex.Deserialize(document.Index, document.Id, source));
        }
    }
}
