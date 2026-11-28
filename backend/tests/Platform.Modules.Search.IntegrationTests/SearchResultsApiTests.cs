using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs;
using Platform.Modules.Blogs.Contracts;
using Platform.Testing.Api;

namespace Platform.Modules.Search.IntegrationTests;

/// <summary>
/// E14-03 (FR-SEARCH, ADR-0124): <c>GET /api/v1/search/blogs</c> and <c>/search/posts</c> — the blocks of the results
/// page, page by page: personal blogs and topics apart, with the reader's subscriptions and own blog marked; posts with
/// their blog, in the titles only, for a period, by date; the black list applies; a bad filter is a validation error.
/// Every test words its documents with a word of its own, since the index is shared by the class.
/// </summary>
public sealed class SearchResultsApiTests(SearchApi api) : IClassFixture<SearchApi>, IAsyncLifetime
{
    private readonly Guid _anna = Guid.CreateVersion7();
    private readonly Guid _boris = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task BlogsAndTopicsComeApartWithTheReadersSubscriptionAndOwnBlogMarked()
    {
        var anna = await NamedBlogAsync(_anna, "Анна и вязание");
        var boris = await NamedBlogAsync(_boris, "Борис и вязание");
        var topic = Subsite.CreateTopic("Вязание", $"knit{_boris.ToString("N")[^8..]}", _boris, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        await api.Index.PutAsync(new IndexedBlog(anna, IndexedBlog.PersonalKind, _anna, "Анна и вязание", null, null), Ct);
        await api.Index.PutAsync(new IndexedBlog(boris, IndexedBlog.PersonalKind, _boris, "Борис и вязание", null, null), Ct);
        await api.Index.PutAsync(new IndexedBlog(topic.Id, IndexedBlog.TopicKind, _boris, "Вязание", null, null), Ct);
        using var client = api.CreateClient();
        using (var subscribe = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/blogs/{boris}/subscription"))
        {
            subscribe.As(_anna.ToString("D"));
            using var done = await client.SendAsync(subscribe, Ct);
            Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        }

        using var response = await SendAsync(client, "/api/v1/search/blogs?q=вязание&kind=personal", _anna);
        var topics = await ReadAsync(client, "/api/v1/search/blogs?q=вязание&kind=topic", _anna);
        var forGuest = await ReadAsync(client, "/api/v1/search/blogs?q=вязание&kind=personal", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        var personal = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.False(personal.GetProperty("hasMore").GetBoolean());
        var rows = personal.GetProperty("items").EnumerateArray().ToDictionary(r => r.GetProperty("id").GetGuid());
        Assert.Equal(new[] { anna, boris }.Order(), rows.Keys.Order());
        Assert.True(rows[anna].GetProperty("own").GetBoolean());
        Assert.False(rows[anna].GetProperty("subscribed").GetBoolean());
        Assert.False(rows[boris].GetProperty("own").GetBoolean());
        Assert.True(rows[boris].GetProperty("subscribed").GetBoolean());
        Assert.Equal("personal", rows[boris].GetProperty("type").GetString());
        var topicRow = Assert.Single(topics.GetProperty("items").EnumerateArray());
        Assert.Equal(topic.Id, topicRow.GetProperty("id").GetGuid());
        Assert.Equal("topic", topicRow.GetProperty("type").GetString());
        Assert.All(forGuest.GetProperty("items").EnumerateArray(), r =>
        {
            Assert.False(r.GetProperty("subscribed").GetBoolean());
            Assert.False(r.GetProperty("own").GetBoolean());
        });
    }

    [Fact]
    public async Task BlogsComePageByPage()
    {
        for (var i = 0; i < SearchResultEndpoints.BlogsPageSize + 2; i++)
        {
            var owner = Guid.CreateVersion7();
            var blog = await NamedBlogAsync(owner, $"Пазлы {i}");
            await api.Index.PutAsync(new IndexedBlog(blog, IndexedBlog.PersonalKind, owner, $"Пазлы {i}", null, null), Ct);
        }

        using var client = api.CreateClient();
        var first = await ReadAsync(client, "/api/v1/search/blogs?q=пазлы&kind=personal", null);
        var second = await ReadAsync(
            client, $"/api/v1/search/blogs?q=пазлы&kind=personal&offset={SearchResultEndpoints.BlogsPageSize}", null);

        Assert.Equal(SearchResultEndpoints.BlogsPageSize, first.GetProperty("items").GetArrayLength());
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Equal(2, second.GetProperty("items").GetArrayLength());
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        Assert.Empty(Ids(first).Intersect(Ids(second)));
    }

    [Fact]
    public async Task PostsComeWithTheirBlogFilteredByTitlePeriodAndBlackListAndByDate()
    {
        var now = api.Time.GetUtcNow();
        var blog = await NamedBlogAsync(_boris, "Борис о сыре");
        var annaBlog = await NamedBlogAsync(_anna, "Анна о сыре");
        var old = new IndexedPost(Guid.CreateVersion7(), blog, _boris, "Сыр за месяц", "Текст", now.AddDays(-20));
        var fresh = new IndexedPost(Guid.CreateVersion7(), blog, _boris, null, "Сварил сыр\nсам", now.AddHours(-3));
        var annas = new IndexedPost(Guid.CreateVersion7(), annaBlog, _anna, "Сыр Анны", "Текст", now.AddHours(-1));
        foreach (var post in new[] { old, fresh, annas })
        {
            await api.Index.PutAsync(post, Ct);
        }

        using var client = api.CreateClient();
        using (var block = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/preferences/me/blocks/{_anna}"))
        {
            block.As(_boris.ToString("D"));
            using var blocked = await client.SendAsync(block, Ct);
            Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        }

        var byDate = await ReadAsync(client, "/api/v1/search/posts?q=сыр&order=date", null);
        var titles = await ReadAsync(client, "/api/v1/search/posts?q=сыр&titlesOnly=true", null);
        var week = await ReadAsync(client, "/api/v1/search/posts?q=сыр&period=week", null);
        var forBoris = await ReadAsync(client, "/api/v1/search/posts?q=сыр", _boris);

        Assert.Equal([annas.Id, fresh.Id, old.Id], Ids(byDate));
        Assert.Equal(new[] { old.Id, annas.Id }.Order(), Ids(titles).Order());
        Assert.Equal(new[] { fresh.Id, annas.Id }.Order(), Ids(week).Order());
        Assert.Equal(new[] { old.Id, fresh.Id }.Order(), Ids(forBoris).Order());
        var shortPost = byDate.GetProperty("items")[1];
        Assert.Equal(JsonValueKind.Null, shortPost.GetProperty("title").ValueKind);
        Assert.Equal("Сварил сыр сам", shortPost.GetProperty("excerpt").GetString());
        Assert.Equal("Борис о сыре", shortPost.GetProperty("blog").GetProperty("name").GetString());
        Assert.Equal("personal", shortPost.GetProperty("blog").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task NothingFoundIsAnEmptyPage()
    {
        using var client = api.CreateClient();

        var posts = await ReadAsync(client, "/api/v1/search/posts?q=щщщнетничего", _anna);
        var blogs = await ReadAsync(client, "/api/v1/search/blogs?q=щщщнетничего&kind=topic", _anna);

        Assert.Empty(posts.GetProperty("items").EnumerateArray());
        Assert.False(posts.GetProperty("hasMore").GetBoolean());
        Assert.Empty(blogs.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("/api/v1/search/blogs?q=сыр", "kind")]
    [InlineData("/api/v1/search/blogs?q=сыр&kind=blog", "kind")]
    [InlineData("/api/v1/search/blogs?kind=topic", "q")]
    [InlineData("/api/v1/search/posts?q=%20", "q")]
    [InlineData("/api/v1/search/posts?q=сыр&period=century", "period")]
    [InlineData("/api/v1/search/posts?q=сыр&order=rating", "order")]
    [InlineData("/api/v1/search/posts?q=сыр&offset=-1", "offset")]
    [InlineData("/api/v1/search/posts?q=сыр&offset=501", "offset")]
    public async Task BadQueryOrFilterIsAValidationError(string path, string field)
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, path, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    // The personal blog of an account, with a name.
    private async Task<Guid> NamedBlogAsync(Guid account, string name)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var blog = await scope.ServiceProvider.GetRequiredService<IBlogDirectory>().EnsurePersonalBlogAsync(account, Ct);
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        var subsite = await db.Subsites.SingleAsync(s => s.Id == blog.Id, Ct);
        subsite.Rename(name);
        await db.SaveChangesAsync(Ct);
        return blog.Id;
    }

    private static List<Guid> Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid())];

    private static async Task<JsonElement> ReadAsync(HttpClient client, string path, Guid? account)
    {
        using var response = await SendAsync(client, path, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, Guid? account)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await client.SendAsync(request, Ct);
    }
}
