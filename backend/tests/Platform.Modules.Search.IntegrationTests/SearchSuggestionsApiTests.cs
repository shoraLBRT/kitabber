using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs.Contracts;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Search.IntegrationTests;

/// <summary>The Api host on PostgreSQL and Redis in Testcontainers with the fake search engine, signed in by the test scheme.</summary>
public sealed class SearchApi : IntegrationApiFactory
{
    public SearchApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
        WithSetting(SearchModule.ProviderKey, nameof(SearchProvider.InMemory));
        WithModule(services => services.AddTestAuthentication());
    }

    public InMemorySearchIndex Index => Services.GetRequiredService<InMemorySearchIndex>();
}

/// <summary>
/// E14-02 (FR-SEARCH, ADR-0097): <c>GET /api/v1/search/suggestions?q=</c> — blogs and posts for a query being typed, for
/// a guest and a signed-in reader alike, with the blog's name as it is now; a reader does not see the posts and the
/// personal blog of an account they blocked, another reader does; a blank or too long query is a validation error.
/// Every test words its documents with a word of its own, since the index is shared by the class.
/// </summary>
public sealed class SearchSuggestionsApiTests(SearchApi api) : IClassFixture<SearchApi>, IAsyncLifetime
{
    private const string _path = "/api/v1/search/suggestions";
    private static readonly DateTimeOffset _at = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _anna = Guid.CreateVersion7();
    private readonly Guid _boris = Guid.CreateVersion7();
    private readonly Guid _vera = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GuestGetsBlogsAndPostsByTheStartOfTheLastWord()
    {
        var blog = await NamedBlogAsync(_boris, "Борис и аквариумы");
        // Indexed under an old name: the suggestion shows the name the blog has now.
        await api.Index.PutAsync(new IndexedBlog(blog, IndexedBlog.PersonalKind, _boris, "Аквариумы", "boris", null), Ct);
        // In the index but gone from Blogs since: left out.
        await api.Index.PutAsync(
            new IndexedBlog(Guid.CreateVersion7(), IndexedBlog.PersonalKind, Guid.NewGuid(), "Аквариумы-2", null, null), Ct);
        var post = new IndexedPost(Guid.CreateVersion7(), blog, _boris, "Мой первый аквариум", "Рыбки и   растения.", _at);
        var shortPost = new IndexedPost(Guid.CreateVersion7(), blog, _boris, null, "Купил аквариум\nна 200 литров", _at);
        await api.Index.PutAsync(post, Ct);
        await api.Index.PutAsync(shortPost, Ct);
        using var client = api.CreateClient();

        using var response = await SendAsync(client, "  аквари  ", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("аквари", body.GetProperty("query").GetString());
        var found = Assert.Single(body.GetProperty("blogs").EnumerateArray());
        Assert.Equal(blog, found.GetProperty("id").GetGuid());
        Assert.Equal("personal", found.GetProperty("kind").GetString());
        Assert.Equal("Борис и аквариумы", found.GetProperty("name").GetString());
        var posts = body.GetProperty("posts").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetGuid());
        Assert.Equal(new[] { post.Id, shortPost.Id }.Order(), posts.Keys.Order());
        Assert.Equal("Мой первый аквариум", posts[post.Id].GetProperty("title").GetString());
        Assert.Equal("Рыбки и растения.", posts[post.Id].GetProperty("excerpt").GetString());
        Assert.Equal(blog, posts[post.Id].GetProperty("blogId").GetGuid());
        Assert.Equal(JsonValueKind.Null, posts[shortPost.Id].GetProperty("title").ValueKind);
        Assert.Equal("Купил аквариум на 200 литров", posts[shortPost.Id].GetProperty("excerpt").GetString());
    }

    [Fact]
    public async Task BlockedAccountsBlogAndPostsAreLeftOutForTheReaderWhoBlockedIt()
    {
        var boris = await NamedBlogAsync(_boris, "Борис о телескопах");
        var vera = await NamedBlogAsync(_vera, "Вера о телескопах");
        await api.Index.PutAsync(new IndexedBlog(boris, IndexedBlog.PersonalKind, _boris, "Борис о телескопах", null, null), Ct);
        await api.Index.PutAsync(new IndexedBlog(vera, IndexedBlog.PersonalKind, _vera, "Вера о телескопах", null, null), Ct);
        var borisPost = new IndexedPost(Guid.CreateVersion7(), boris, _boris, "Телескоп за выходные", "Текст", _at);
        var veraPost = new IndexedPost(Guid.CreateVersion7(), vera, _vera, "Телескоп для детей", "Текст", _at);
        await api.Index.PutAsync(borisPost, Ct);
        await api.Index.PutAsync(veraPost, Ct);
        using var client = api.CreateClient();
        using (var block = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/preferences/me/blocks/{_boris}"))
        {
            block.As(_anna.ToString("D"));
            using var blocked = await client.SendAsync(block, Ct);
            Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        }

        var forAnna = await ReadAsync(client, "телеско", _anna);
        var forVera = await ReadAsync(client, "телеско", _vera);

        Assert.Equal([vera], Ids(forAnna, "blogs"));
        Assert.Equal([veraPost.Id], Ids(forAnna, "posts"));
        Assert.Equal(new[] { boris, vera }.Order(), Ids(forVera, "blogs").Order());
        Assert.Equal(new[] { borisPost.Id, veraPost.Id }.Order(), Ids(forVera, "posts").Order());
    }

    [Fact]
    public async Task NothingFoundIsEmptyLists()
    {
        using var client = api.CreateClient();

        var body = await ReadAsync(client, "щщщнетничего", _anna);

        Assert.Empty(body.GetProperty("blogs").EnumerateArray());
        Assert.Empty(body.GetProperty("posts").EnumerateArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task BlankQueryIsAValidationError(string? query)
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, query, null);

        await AssertValidationErrorAsync(response);
    }

    [Fact]
    public async Task TooLongQueryIsAValidationError()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, new string('а', SearchEndpoints.MaxQueryLength + 1), _anna);

        await AssertValidationErrorAsync(response);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("q", out _));
    }

    // The personal blog of an account, with a name.
    private async Task<Guid> NamedBlogAsync(Guid account, string name)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var blog = await scope.ServiceProvider.GetRequiredService<IBlogDirectory>().EnsurePersonalBlogAsync(account, Ct);
        var db = scope.ServiceProvider.GetRequiredService<Blogs.BlogsDbContext>();
        var subsite = await db.Subsites.SingleAsync(s => s.Id == blog.Id, Ct);
        subsite.Rename(name);
        await db.SaveChangesAsync(Ct);
        return blog.Id;
    }

    private static IEnumerable<Guid> Ids(JsonElement body, string list) =>
        body.GetProperty(list).EnumerateArray().Select(e => e.GetProperty("id").GetGuid());

    private static async Task<JsonElement> ReadAsync(HttpClient client, string query, Guid? account)
    {
        using var response = await SendAsync(client, query, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string? query, Guid? account)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, query is null ? _path : $"{_path}?q={Uri.EscapeDataString(query)}");
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await client.SendAsync(request, Ct);
    }
}
