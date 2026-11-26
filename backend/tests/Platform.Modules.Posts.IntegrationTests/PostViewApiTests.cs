using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Testing.Api;
using Platform.Testing.Data;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E07-06: the view counter through <c>/api/v1/posts/{id}/open</c>, <c>/api/v1/posts/impressions</c> and
/// <c>/api/v1/posts/{id}/reading</c> (ADR-0071) — a reader counts once a day, bots and the author never, and the count
/// is on the post and on its card in the feed. Counts go through the Redis buffer and reach the database when it is
/// flushed (ADR-0083); the flush itself is covered by <see cref="ViewFlushTests"/>.
/// </summary>
public sealed class PostViewApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private const string _chrome =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36";

    private const string _firefox = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14.6; rv:133.0) Gecko/20100101 Firefox/133.0";

    private HttpClient _client = null!;
    private Guid _author;
    private Guid _reader;
    private Guid _other;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = Guid.CreateVersion7(api.Time.GetUtcNow());
        _reader = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(1));
        _other = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(2));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReaderCountsOnceADay()
    {
        var post = await SavePostAsync(publish: true);

        for (var i = 0; i < 3; i++)
        {
            using var response = await OpenAsync(post.Id, _reader);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        (await OpenAsync(post.Id, _other)).Dispose();
        Assert.Equal(2, await ViewCountAsync(post.Id));

        api.Time.Advance(TimeSpan.FromDays(1));
        (await OpenAsync(post.Id, _reader)).Dispose();
        (await OpenAsync(post.Id, _reader)).Dispose();

        Assert.Equal(3, await ViewCountAsync(post.Id));
    }

    [Fact]
    public async Task GuestCountsOnceADayByNetworkAndBrowser()
    {
        var post = await SavePostAsync(publish: true);

        (await OpenAsync(post.Id, account: null)).Dispose();
        (await OpenAsync(post.Id, account: null)).Dispose();
        Assert.Equal(1, await ViewCountAsync(post.Id));

        (await OpenAsync(post.Id, account: null, _firefox)).Dispose();
        Assert.Equal(2, await ViewCountAsync(post.Id));

        await FlushAsync();
        await using var scope = api.Services.CreateAsyncScope();
        var viewers = await scope.ServiceProvider.GetRequiredService<PostsDbContext>().ViewMarks
            .Select(m => m.Viewer).ToListAsync(Ct);
        Assert.All(viewers, v => Assert.StartsWith("g:", v, StringComparison.Ordinal));
        Assert.All(viewers, v => Assert.DoesNotContain("Mozilla", v, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
    [InlineData("curl/8.5.0")]
    [InlineData("")]
    public async Task BotsAreNotCounted(string userAgent)
    {
        var post = await SavePostAsync(publish: true);

        using var asGuest = await OpenAsync(post.Id, account: null, userAgent);
        using var asAccount = await OpenAsync(post.Id, _reader, userAgent);
        using var impressions = await SendAsync(
            HttpMethod.Post, "/api/v1/posts/impressions", $$"""{"postIds": ["{{post.Id}}"]}""", _reader, userAgent);

        Assert.Equal(HttpStatusCode.NoContent, asGuest.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, asAccount.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, impressions.StatusCode);
        Assert.Equal(0, await ViewCountAsync(post.Id));
        Assert.Equal(0, await MarksAsync());
    }

    [Fact]
    public async Task PrefetchIsNotCounted()
    {
        var post = await SavePostAsync(publish: true);
        using var request = Request(HttpMethod.Post, $"/api/v1/posts/{post.Id}/open", null, _reader, _chrome);
        request.Headers.Add("Sec-Purpose", "prefetch");

        using var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await ViewCountAsync(post.Id));
    }

    [Fact]
    public async Task AuthorIsNotCounted()
    {
        var post = await SavePostAsync(publish: true);

        (await OpenAsync(post.Id, _author)).Dispose();

        Assert.Equal(0, await ViewCountAsync(post.Id));
    }

    [Fact]
    public async Task DraftAndUnknownPostAreNotFound()
    {
        var draft = await SavePostAsync(publish: false);

        using var ofDraft = await OpenAsync(draft.Id, _reader);
        using var unknown = await OpenAsync(Guid.CreateVersion7(), _reader);
        using var reading = await SendAsync(
            HttpMethod.Post, $"/api/v1/posts/{draft.Id}/reading", """{"seconds": 30}""", _reader);

        Assert.Equal(HttpStatusCode.NotFound, ofDraft.StatusCode);
        Assert.Equal(PostEndpoints.PostNotFoundCode, await CodeOf(ofDraft));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reading.StatusCode);
    }

    [Fact]
    public async Task CardCarriesTheViewCount()
    {
        var post = await SavePostAsync(publish: true);
        (await OpenAsync(post.Id, _reader)).Dispose();
        (await OpenAsync(post.Id, _other)).Dispose();

        await FlushAsync();
        var feed = await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh", Ct);

        var card = feed.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("post").GetProperty("id").GetGuid() == post.Id);
        Assert.Equal(2, card.GetProperty("post").GetProperty("viewCount").GetInt32());
    }

    [Fact]
    public async Task ImpressionsCountEachCardOnceADay()
    {
        var first = await SavePostAsync(publish: true);
        var second = await SavePostAsync(publish: true);
        var own = await SavePostAsync(publish: true, author: _reader);
        var body = $$"""{"postIds": ["{{first.Id}}", "{{second.Id}}", "{{first.Id}}", "{{own.Id}}"]}""";

        using var response = await SendAsync(HttpMethod.Post, "/api/v1/posts/impressions", body, _reader);
        (await SendAsync(HttpMethod.Post, "/api/v1/posts/impressions", body, _reader)).Dispose();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal((1, 1, 0), (await ImpressionsAsync(first.Id), await ImpressionsAsync(second.Id),
            await ImpressionsAsync(own.Id)));
        Assert.Equal(0, await ViewCountAsync(first.Id));
    }

    [Theory]
    [InlineData("""{"postIds": []}""")]
    [InlineData("""{}""")]
    public async Task ImpressionsNeedOneToFiftyPosts(string body)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/v1/posts/impressions", body, _reader);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TooManyImpressionsAreRefused()
    {
        var ids = string.Join(", ", Enumerable.Range(0, ViewRules.MaxImpressions + 1).Select(_ => $"\"{Guid.NewGuid()}\""));

        using var response = await SendAsync(
            HttpMethod.Post, "/api/v1/posts/impressions", $$"""{"postIds": [{{ids}}]}""", _reader);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReadingKeepsTheLongestTimeOfTheCountedOpening()
    {
        var post = await SavePostAsync(publish: true);
        (await OpenAsync(post.Id, _reader)).Dispose();

        using var first = await SendAsync(
            HttpMethod.Post, $"/api/v1/posts/{post.Id}/reading", """{"seconds": 90}""", _reader);
        (await SendAsync(HttpMethod.Post, $"/api/v1/posts/{post.Id}/reading", """{"seconds": 40}""", _reader))
            .Dispose();
        (await SendAsync(HttpMethod.Post, $"/api/v1/posts/{post.Id}/reading", """{"seconds": 60}""", _author))
            .Dispose();

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await FlushAsync();
        await using var scope = api.Services.CreateAsyncScope();
        var marks = await scope.ServiceProvider.GetRequiredService<PostsDbContext>().ViewMarks
            .Select(m => m.ReadSeconds).ToListAsync(Ct);
        Assert.Equal([90], marks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ViewRules.MaxReadSeconds + 1)]
    public async Task ReadingOutsideTheLimitsIsRefused(int seconds)
    {
        var post = await SavePostAsync(publish: true);

        using var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/posts/{post.Id}/reading", $$"""{"seconds": {{seconds}}}""", _reader);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Post> SavePostAsync(bool publish, Guid? author = null)
    {
        var post = new PostFactory(api.Data).Build();
        var mine = Post.CreateDraft(author ?? _author, post.BlogId, post.Title, post.Body, api.Time.GetUtcNow());
        if (publish)
        {
            mine.Publish(api.Time.GetUtcNow());
        }

        await api.SaveAsync<PostsDbContext>(mine);
        return mine;
    }

    private Task<HttpResponseMessage> OpenAsync(Guid postId, Guid? account, string userAgent = _chrome) =>
        SendAsync(HttpMethod.Post, $"/api/v1/posts/{postId}/open", null, account, userAgent);

    private async Task<int> ViewCountAsync(Guid postId)
    {
        await FlushAsync();
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PostsDbContext>().Posts
            .Where(p => p.Id == postId).Select(p => p.ViewCount).SingleAsync(Ct);
    }

    private async Task<int> ImpressionsAsync(Guid postId)
    {
        await FlushAsync();
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PostsDbContext>().Posts
            .Where(p => p.Id == postId).Select(p => p.ImpressionCount).SingleAsync(Ct);
    }

    private async Task<int> MarksAsync()
    {
        await FlushAsync();
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PostsDbContext>().ViewMarks.CountAsync(Ct);
    }

    // Views reach the database with the flush of the buffer (ADR-0083); the test does not wait for the timer.
    private Task<int> FlushAsync() => api.Services.GetRequiredService<ViewFlusher>().FlushAsync(Ct);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? content, Guid? account, string userAgent = _chrome)
    {
        using var request = Request(method, path, content, account, userAgent);
        return await _client.SendAsync(request, Ct);
    }

    private static HttpRequestMessage Request(
        HttpMethod method, string path, string? content, Guid? account, string userAgent)
    {
        var request = new HttpRequestMessage(method, path);
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        if (userAgent.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }

        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return request;
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
