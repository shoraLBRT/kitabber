using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Modules.Blogs;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E11-01: the feed of one blog <c>/api/v1/feeds/blogs/{blogId}</c> — the topic page's list (ADR-0082) — gives the
/// blog's published posts only, newest first, page by page with the cursor of the fresh feed (ADR-0043).
/// </summary>
public sealed class BlogFeedApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Subsite _topic = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var now = api.Time.GetUtcNow();
        _author = Guid.CreateVersion7(now);
        _topic = Subsite.CreateTopic("Наука", $"science{_author.ToString("N")[..8]}", Guid.CreateVersion7(now), now);
        await api.SaveAsync<BlogsDbContext>(_topic);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnyoneReadsTheTopicsPublishedPostsNewestFirstPageByPage()
    {
        var published = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            api.Time.Advance(TimeSpan.FromMinutes(1));
            published.Add(await SaveAsync(_topic.Id, publishedAt: api.Time.GetUtcNow()));
        }

        var (first, cursor) = await PageAsync(_topic.Id, "?limit=2");
        var (last, end) = await PageAsync(_topic.Id, $"?limit=2&cursor={Uri.EscapeDataString(cursor!)}");

        Assert.Equal(published.AsEnumerable().Reverse(), [.. first, .. last]);
        Assert.Null(end);
    }

    [Fact]
    public async Task OtherBlogsDraftsAndDeletedPostsAreNotInIt()
    {
        var shown = await SaveAsync(_topic.Id, publishedAt: api.Time.GetUtcNow());
        await SaveAsync(_topic.Id, publishedAt: null);
        await SaveAsync(_topic.Id, publishedAt: api.Time.GetUtcNow(), delete: true);
        var elsewhere = Subsite.CreatePersonal(_author, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(elsewhere);
        var own = await SaveAsync(elsewhere.Id, publishedAt: api.Time.GetUtcNow());

        Assert.Equal([shown], (await PageAsync(_topic.Id)).Items);
        Assert.Equal([own], (await PageAsync(elsewhere.Id)).Items);
    }

    [Fact]
    public async Task EmptyTopicIsAnEmptyLastPage()
    {
        var (items, cursor) = await PageAsync(_topic.Id);

        Assert.Empty(items);
        Assert.Null(cursor);
    }

    [Fact]
    public async Task UnknownBlogIsNotFound()
    {
        using var response = await _client.GetAsync($"/api/v1/feeds/blogs/{Guid.CreateVersion7()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(BlogFeedEndpoints.BlogNotFoundCode, await CodeOf(response));
    }

    [Theory]
    [InlineData("?limit=0", "validation_failed")]
    [InlineData("?cursor=not-a-cursor", "invalid_cursor")]
    public async Task WrongPageIsABadRequest(string query, string code)
    {
        using var response = await _client.GetAsync($"/api/v1/feeds/blogs/{_topic.Id}{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await CodeOf(response));
    }

    private async Task<Guid> SaveAsync(Guid blog, DateTimeOffset? publishedAt, bool delete = false)
    {
        var post = Post.CreateDraft(_author, blog, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
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

    private async Task<(List<Guid> Items, string? Cursor)> PageAsync(Guid blog, string query = "")
    {
        var page = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/feeds/blogs/{blog}{query}", Ct);
        var next = page.GetProperty("nextCursor");
        return ([.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())],
            next.ValueKind == JsonValueKind.Null ? null : next.GetString());
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
