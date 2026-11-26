using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Modules.Blogs;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E08-01: the fresh feed <c>/api/v1/feeds/fresh</c> gives every published post, newest first, page by page with a
/// cursor, with no post skipped or repeated when new posts are published between pages (ADR-0043).
/// </summary>
public sealed class FreshFeedApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _blog;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = Guid.CreateVersion7(api.Time.GetUtcNow());
        _blog = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(1));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnyoneReadsPublishedPostsNewestFirstPageByPage()
    {
        var published = await PublishAsync(5);

        var (first, cursor) = await PageAsync(limit: 2);
        var (second, next) = await PageAsync(limit: 2, cursor);
        var (last, end) = await PageAsync(limit: 2, next);

        Assert.Equal(published.AsEnumerable().Reverse(), [.. first, .. second, .. last]);
        Assert.Equal(2, first.Count);
        Assert.Single(last);
        Assert.Null(end);
    }

    [Fact]
    public async Task NewPostsBetweenPagesAreNeitherSkippedNorRepeated()
    {
        var older = await PublishAsync(4);
        var (first, cursor) = await PageAsync(limit: 2);

        var newer = await PublishAsync(3);
        var (second, end) = await PageAsync(limit: 2, cursor);

        Assert.Equal([older[3], older[2]], first);
        Assert.Equal([older[1], older[0]], second);
        Assert.Null(end);
        // The new posts are at the top of a fresh first page.
        var (top, _) = await PageAsync(limit: 3);
        Assert.Equal(newer.AsEnumerable().Reverse(), top);
    }

    [Fact]
    public async Task PostsPublishedAtTheSameMomentAreOrderedByIdAcrossPages()
    {
        var at = api.Time.GetUtcNow();
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await SaveAsync(publishedAt: at));
        }

        var (first, cursor) = await PageAsync(limit: 2);
        var (second, next) = await PageAsync(limit: 2, cursor);
        var (last, _) = await PageAsync(limit: 2, next);

        Assert.Equal(5, new HashSet<Guid>([.. first, .. second, .. last]).Count);
        Assert.Equal(ids.Order().Reverse(), [.. first, .. second, .. last]);
    }

    [Fact]
    public async Task DraftsAndDeletedPostsAreNotInTheFeed()
    {
        var shown = await SaveAsync(publishedAt: api.Time.GetUtcNow());
        await SaveAsync(publishedAt: null);
        await SaveAsync(publishedAt: api.Time.GetUtcNow(), delete: true);

        var (items, cursor) = await PageAsync(limit: 10);

        Assert.Equal([shown], items);
        Assert.Null(cursor);
    }

    [Fact]
    public async Task EmptyFeedIsAnEmptyLastPage()
    {
        var (items, cursor) = await PageAsync(limit: null);

        Assert.Empty(items);
        Assert.Null(cursor);
    }

    [Fact]
    public async Task FeedItemIsThePostView()
    {
        await SaveAsync(publishedAt: api.Time.GetUtcNow());

        var page = await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh", Ct);

        var item = page.GetProperty("items")[0].GetProperty("post");
        Assert.Equal("published", item.GetProperty("status").GetString());
        Assert.Equal(_blog, item.GetProperty("blogId").GetGuid());
        Assert.Equal("paragraph", item.GetProperty("body").GetProperty("blocks")[0].GetProperty("type").GetString());
    }

    // E08-02, ADR-0052: the card shows the author's personal blog and, for a post in a topic, the topic.
    [Fact]
    public async Task FeedItemCarriesTheAuthorAndTheTopic()
    {
        var own = Subsite.CreatePersonal(_author, api.Time.GetUtcNow());
        own.Rename("Анна");
        var topic = Subsite.CreateTopic("Игры", "games", Guid.CreateVersion7(api.Time.GetUtcNow()), api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(own);
        await api.SaveAsync<BlogsDbContext>(topic);
        await SaveAsync(publishedAt: api.Time.GetUtcNow(), blog: own.Id);
        await SaveAsync(publishedAt: api.Time.GetUtcNow().AddMinutes(1), blog: topic.Id);

        var items = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh", Ct)).GetProperty("items");

        var inTopic = items[0];
        Assert.Equal(own.Id, inTopic.GetProperty("author").GetProperty("id").GetGuid());
        Assert.Equal("Анна", inTopic.GetProperty("author").GetProperty("name").GetString());
        Assert.Equal("personal", inTopic.GetProperty("author").GetProperty("type").GetString());
        Assert.Equal(topic.Id, inTopic.GetProperty("topic").GetProperty("id").GetGuid());
        Assert.Equal("games", inTopic.GetProperty("topic").GetProperty("nickname").GetString());
        Assert.Equal("topic", inTopic.GetProperty("topic").GetProperty("type").GetString());
        var inBlog = items[1];
        Assert.Equal(own.Id, inBlog.GetProperty("author").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, inBlog.GetProperty("topic").ValueKind);
        Assert.Empty(inBlog.GetProperty("author").GetProperty("avatarSources").EnumerateArray());
    }

    [Fact]
    public async Task FeedItemOfAnAccountWithoutABlogHasNoAuthor()
    {
        await SaveAsync(publishedAt: api.Time.GetUtcNow());

        var item = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh", Ct)).GetProperty("items")[0];

        Assert.Equal(JsonValueKind.Null, item.GetProperty("author").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("topic").ValueKind);
    }

    [Theory]
    [InlineData("?limit=0", "validation_failed")]
    [InlineData("?limit=101", "validation_failed")]
    [InlineData("?cursor=not-a-cursor", "invalid_cursor")]
    public async Task WrongPageIsABadRequest(string query, string code)
    {
        using var response = await _client.GetAsync($"/api/v1/feeds/fresh{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    // Publishes count posts a minute apart, the oldest first; returns their ids in that order.
    private async Task<List<Guid>> PublishAsync(int count)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            api.Time.Advance(TimeSpan.FromMinutes(1));
            ids.Add(await SaveAsync(publishedAt: api.Time.GetUtcNow()));
        }

        return ids;
    }

    private async Task<Guid> SaveAsync(DateTimeOffset? publishedAt, bool delete = false, Guid? blog = null)
    {
        var post = Post.CreateDraft(_author, blog ?? _blog, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        if (publishedAt is { } at)
        {
            post.Publish(at);
        }

        if (delete)
        {
            post.Delete(api.Time.GetUtcNow());
        }

        await api.SaveAsync<PostsDbContext>(post);
        return post.Id;
    }

    private async Task<(List<Guid> Items, string? Cursor)> PageAsync(int? limit, string? cursor = null)
    {
        var query = new List<string>();
        if (limit is { } l)
        {
            query.Add($"limit={l}");
        }

        if (cursor is not null)
        {
            query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        var page = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/feeds/fresh{(query.Count > 0 ? "?" + string.Join('&', query) : string.Empty)}", Ct);
        var next = page.GetProperty("nextCursor");
        return ([.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())],
            next.ValueKind == JsonValueKind.Null ? null : next.GetString());
    }
}
