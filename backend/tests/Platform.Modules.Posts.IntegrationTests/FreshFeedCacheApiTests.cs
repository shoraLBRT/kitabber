using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Platform.Modules.Identity;
using Platform.Testing.Api;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E08-09: the fresh feed is given from Redis (ADR-0080) — a repeated read sends no SQL command, publishing, editing
/// and deleting a post drop the cached pages at once, and a page is read from the database again once it is older
/// than <see cref="FreshFeedCache.Lifetime"/>.
/// </summary>
public sealed class FreshFeedCacheApiTests(FeedApi api) : IClassFixture<FeedApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _blog;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var account = new Account
        {
            Id = Guid.CreateVersion7(api.Time.GetUtcNow()),
            CreatedAt = api.Time.GetUtcNow(),
        };
        account.ConfirmEmail("feed-cache@example.com", "feed-cache@example.com", api.Time.GetUtcNow());
        await api.SaveAsync<IdentityDbContext>(account);
        _author = account.Id;
        _blog = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(1));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RepeatedReadDoesNotTouchTheDatabase()
    {
        await SaveAsync(published: true);
        await SaveAsync(published: true);
        var first = await RawFeedAsync("?limit=2");
        var firstNext = await RawFeedAsync($"?limit=1&cursor={Uri.EscapeDataString(await CursorAsync("?limit=1"))}");

        api.Commands.Reset();
        var again = await RawFeedAsync("?limit=2");
        var againNext = await RawFeedAsync($"?limit=1&cursor={Uri.EscapeDataString(await CursorAsync("?limit=1"))}");

        Assert.Equal(0, api.Commands.Count);
        Assert.Equal(first, again);
        Assert.Equal(firstNext, againNext);
    }

    [Fact]
    public async Task PageIsCachedUntilItsLifetimeEnds()
    {
        var old = await SaveAsync(published: true);
        Assert.Equal([old], await FeedAsync());

        // Written past the API, so nothing drops the page: the cache gives it as it was.
        var unseen = await SaveAsync(published: true);
        Assert.Equal([old], await FeedAsync());

        api.Time.Advance(FreshFeedCache.Lifetime);
        Assert.Equal([unseen, old], await FeedAsync());
    }

    [Fact]
    public async Task PublishingAPostDropsTheCachedPages()
    {
        var old = await SaveAsync(published: true);
        var draft = await CreateAsync("Черновик");
        Assert.Equal([old], await FeedAsync());

        using var published = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{draft}/publish", null);

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal([draft, old], await FeedAsync());
    }

    [Fact]
    public async Task DeletingAPostDropsTheCachedPages()
    {
        var kept = await SaveAsync(published: true);
        var gone = await CreateAsync("Удалю");
        (await SendAsync(HttpMethod.Post, $"/api/v1/posts/{gone}/publish", null)).Dispose();
        Assert.Equal([gone, kept], await FeedAsync());

        using var deleted = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{gone}", null);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal([kept], await FeedAsync());
    }

    [Fact]
    public async Task EditingAPublishedPostDropsTheCachedPages()
    {
        var id = await CreateAsync("Было");
        (await SendAsync(HttpMethod.Post, $"/api/v1/posts/{id}/publish", null)).Dispose();
        Assert.Equal("Было", await FirstTitleAsync());

        using var edited = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{id}", Input("Стало"));

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("Стало", await FirstTitleAsync());
    }

    [Fact]
    public async Task WrongPageIsStillABadRequest()
    {
        await FeedAsync();

        using var response = await _client.GetAsync("/api/v1/feeds/fresh?cursor=not-a-cursor", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Guid> SaveAsync(bool published)
    {
        api.Time.Advance(TimeSpan.FromSeconds(1));
        var post = Post.CreateDraft(_author, _blog, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        if (published)
        {
            post.Publish(api.Time.GetUtcNow());
        }

        await api.SaveAsync<PostsDbContext>(post);
        return post.Id;
    }

    private async Task<Guid> CreateAsync(string title)
    {
        api.Time.Advance(TimeSpan.FromSeconds(1));
        using var response = await SendAsync(HttpMethod.Post, "/api/v1/posts", Input(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static string Input(string title) =>
        $$"""{"blogId": null, "title": {{JsonSerializer.Serialize(title)}}, "body": {{PostFactory.Body("Текст.")}}}""";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? content)
    {
        using var request = new HttpRequestMessage(method, path);
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        request.As(_author.ToString("D"));
        return await _client.SendAsync(request, Ct);
    }

    private Task<string> RawFeedAsync(string query) => _client.GetStringAsync($"/api/v1/feeds/fresh{query}", Ct);

    private async Task<string> CursorAsync(string query) =>
        (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/feeds/fresh{query}", Ct))
        .GetProperty("nextCursor").GetString()!;

    private async Task<List<Guid>> FeedAsync() =>
        [.. (await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh", Ct)).GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())];

    private async Task<string?> FirstTitleAsync() =>
        (await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh", Ct)).GetProperty("items")[0]
        .GetProperty("post").GetProperty("title").GetString();
}
