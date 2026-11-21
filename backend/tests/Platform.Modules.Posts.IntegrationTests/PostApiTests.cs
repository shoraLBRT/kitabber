using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs;
using Platform.Modules.Editor.Contracts;
using Platform.Seeding;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>The Api host with the Posts module and its demo seeder, on PostgreSQL and Redis in Testcontainers, signed in by the test scheme.</summary>
public sealed class PostsApi : IntegrationApiFactory
{
    public PostsApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(services =>
        {
            services.AddTestAuthentication();
            services.AddDemoSeeder<PostsDemoSeeder>();
        });
}

/// <summary>
/// E07-01: create, read, edit and delete a post through <c>/api/v1/posts</c>, with the rights of the author, of other
/// accounts and of anonymous readers, and the body checked by the block schemas.
/// </summary>
public sealed class PostApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private const string _validBody = """
        {"version": 1, "blocks": [
          {"id": "p1", "type": "paragraph", "data": {"text": "<a href=\"javascript:alert(1)\">Привет</a>"}}
        ]}
        """;

    private HttpClient _client = null!;
    private Guid _author;
    private Guid _stranger;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = Guid.CreateVersion7(api.Time.GetUtcNow());
        _stranger = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(1));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AuthorCreatesANativeDraftInTheirPersonalBlog()
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/v1/posts", Input(title: " Первый пост "), _author);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var post = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = post.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/posts/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(_author, post.GetProperty("authorAccountId").GetGuid());
        Assert.Equal("Первый пост", post.GetProperty("title").GetString());
        Assert.Equal("draft", post.GetProperty("status").GetString());
        Assert.Equal("native", post.GetProperty("origin").GetString());
        Assert.Equal(JsonValueKind.Null, post.GetProperty("source").ValueKind);
        Assert.Equal(JsonValueKind.Null, post.GetProperty("publishedAt").ValueKind);
        // The body is stored cleaned, as the Editor's validator returned it.
        Assert.Equal("<a rel=\"nofollow ugc noopener noreferrer\">Привет</a>",
            post.GetProperty("body").GetProperty("blocks")[0].GetProperty("data").GetProperty("text").GetString());

        await using var scope = api.Services.CreateAsyncScope();
        var personal = await scope.ServiceProvider.GetRequiredService<PersonalBlogs>().FindAsync(_author, Ct);
        Assert.Equal(personal!.Id, post.GetProperty("blogId").GetGuid());
    }

    [Fact]
    public async Task AnyAccountPostsInATopic()
    {
        var topic = await SaveTopicAsync();

        using var response = await SendAsync(HttpMethod.Post, "/api/v1/posts", Input(blogId: topic.Id), _stranger);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(topic.Id, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("blogId").GetGuid());
    }

    [Fact]
    public async Task NoOnePostsInAnotherAccountsPersonalBlog()
    {
        var blog = await SavePersonalBlogAsync(_author);

        using var response = await SendAsync(HttpMethod.Post, "/api/v1/posts", Input(blogId: blog.Id), _stranger);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("posts.blog_forbidden", await CodeOf(response));
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task UnknownBlogIsAValidationError()
    {
        using var response = await SendAsync(
            HttpMethod.Post, "/api/v1/posts", Input(blogId: Guid.CreateVersion7(api.Time.GetUtcNow())), _author);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["blogId"], await ErrorFieldsOf(response));
    }

    [Fact]
    public async Task BodyOutsideTheBlockSchemasIsAValidationError()
    {
        const string body = """{"version": 1, "blocks": [{"id": "x", "type": "marquee", "data": {}}]}""";

        using var invalid = await SendAsync(HttpMethod.Post, "/api/v1/posts", Input(body: body), _author);
        using var missing = await SendAsync(HttpMethod.Post, "/api/v1/posts", """{"title": "Без тела"}""", _author);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(["body.blocks[0].type"], await ErrorFieldsOf(invalid));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(["body"], await ErrorFieldsOf(missing));
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task CreatingNeedsSignIn()
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/v1/posts", Input(), account: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DraftIsSeenOnlyByItsAuthor()
    {
        var draft = await SavePostAsync(publish: false);

        using var asAuthor = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{draft.Id}", content: null, _author);
        using var asStranger = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{draft.Id}", content: null, _stranger);
        using var anonymous = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{draft.Id}", content: null, account: null);

        Assert.Equal(HttpStatusCode.OK, asAuthor.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asStranger.StatusCode);
        Assert.Equal("posts.post_not_found", await CodeOf(asStranger));
        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
    }

    [Fact]
    public async Task AnyoneReadsAPublishedPost()
    {
        var post = await SavePostAsync(publish: true);

        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}", content: null, account: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("published", view.GetProperty("status").GetString());
        Assert.Equal(post.PublishedAt, view.GetProperty("publishedAt").GetDateTimeOffset());
        Assert.Equal(post.Title, view.GetProperty("title").GetString());
    }

    [Fact]
    public async Task CarriedOverPostShowsItsOriginAndSource()
    {
        var post = Post.CreateImported(
            _author, Guid.CreateVersion7(api.Time.GetUtcNow()), null, PostFactory.Body("Из канала."),
            new PostSource("-1001234567890", "42", new Uri("https://t.me/channel/42")), api.Time.GetUtcNow());
        post.Publish(api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(post);

        var view = await (await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}", content: null, account: null))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal("telegram", view.GetProperty("origin").GetString());
        var source = view.GetProperty("source");
        Assert.Equal("-1001234567890", source.GetProperty("channel").GetString());
        Assert.Equal("42", source.GetProperty("message").GetString());
        Assert.Equal("https://t.me/channel/42", source.GetProperty("url").GetString());
    }

    [Fact]
    public async Task DatabaseKeepsOnePostPerExternalMessage()
    {
        var source = new PostSource("channel", "1", Url: null);
        var blog = Guid.CreateVersion7(api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(
            Post.CreateImported(_author, blog, null, PostFactory.Body("a"), source, api.Time.GetUtcNow()));
        var twin = Post.CreateImported(_author, blog, null, PostFactory.Body("b"), source, api.Time.GetUtcNow());

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => api.SaveAsync<PostsDbContext>(twin));
        Assert.Contains(PostsDbContext.SourceIndex, error.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorEditsTheTitleTheBodyAndTheBlog()
    {
        var post = await SavePostAsync(publish: true);
        var topic = await SaveTopicAsync();
        api.Time.Advance(TimeSpan.FromMinutes(5));
        var body = """{"version": 1, "blocks": [{"id": "h", "type": "header", "data": {"text": "Новое", "level": 2}}]}""";

        using var response = await SendAsync(
            HttpMethod.Put, $"/api/v1/posts/{post.Id}", Input(blogId: topic.Id, title: "", body: body), _author);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(topic.Id, view.GetProperty("blogId").GetGuid());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("title").ValueKind);
        Assert.Equal("header", view.GetProperty("body").GetProperty("blocks")[0].GetProperty("type").GetString());
        Assert.Equal(api.Time.GetUtcNow(), view.GetProperty("updatedAt").GetDateTimeOffset());
        Assert.Equal(post.CreatedAt, view.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal("published", view.GetProperty("status").GetString());
    }

    [Fact]
    public async Task EditWithoutABlogKeepsThePostWhereItIs()
    {
        var post = await SavePostAsync(publish: false);

        using var response = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}", Input(title: "Другой"), _author);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(post.BlogId, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("blogId").GetGuid());
    }

    [Fact]
    public async Task OnlyTheAuthorEditsAPost()
    {
        var published = await SavePostAsync(publish: true);
        var draft = await SavePostAsync(publish: false);
        var others = await SavePersonalBlogAsync(_stranger);

        using var stranger = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{published.Id}", Input(), _stranger);
        using var strangersDraft = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{draft.Id}", Input(), _stranger);
        using var anonymous = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{published.Id}", Input(), account: null);
        using var intoOthersBlog = await SendAsync(
            HttpMethod.Put, $"/api/v1/posts/{published.Id}", Input(blogId: others.Id), _author);
        using var badBody = await SendAsync(
            HttpMethod.Put, $"/api/v1/posts/{published.Id}", Input(body: """{"version": 99, "blocks": []}"""), _author);

        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
        Assert.Equal("posts.not_author", await CodeOf(stranger));
        Assert.Equal(HttpStatusCode.NotFound, strangersDraft.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, intoOthersBlog.StatusCode);
        Assert.Equal("posts.blog_forbidden", await CodeOf(intoOthersBlog));
        Assert.Equal(HttpStatusCode.BadRequest, badBody.StatusCode);
        await using var scope = api.Services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<PostsDbContext>().Posts
            .SingleAsync(p => p.Id == published.Id, Ct);
        Assert.Equal(published.Title, saved.Title);
        Assert.Equal(published.BlogId, saved.BlogId);
    }

    [Fact]
    public async Task AuthorDeletesAPostSoftly()
    {
        var post = await SavePostAsync(publish: true);
        api.Time.Advance(TimeSpan.FromMinutes(1));

        using var deleted = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}", content: null, _author);
        using var read = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}", content: null, _author);
        using var again = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}", content: null, _author);
        using var edit = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}", Input(), _author);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        // The row stays, marked as deleted.
        await using var scope = api.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<PostsDbContext>().Posts.IgnoreQueryFilters()
            .SingleAsync(p => p.Id == post.Id, Ct);
        Assert.Equal(api.Time.GetUtcNow(), row.DeletedAt);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task OnlyTheAuthorDeletesAPost()
    {
        var post = await SavePostAsync(publish: true);

        using var stranger = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}", content: null, _stranger);
        using var anonymous = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}", content: null, account: null);

        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
        Assert.Equal("posts.not_author", await CodeOf(stranger));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task DemoPostsAreSeededOnceWithBodiesTheEditorAccepts()
    {
        Assert.Equal(PostsDemoSeeder.Count, await api.SeedDemoAsync());
        var again = await api.SeedDemoAsync();

        Assert.Equal(0, again);
        Assert.Equal(PostsDemoSeeder.Count, await CountAsync());
        await using var scope = api.Services.CreateAsyncScope();
        var validator = scope.ServiceProvider.GetRequiredService<IBlockDocumentValidator>();
        var bodies = await scope.ServiceProvider.GetRequiredService<PostsDbContext>().Posts.Select(p => p.Body).ToListAsync(Ct);
        Assert.All(bodies, body => Assert.True(validator.Validate(JsonDocument.Parse(body).RootElement).IsValid));
    }

    private static string Input(Guid? blogId = null, string? title = "Заголовок", string body = _validBody) =>
        $$"""{"blogId": {{(blogId is { } id ? $"\"{id}\"" : "null")}}, "title": {{JsonSerializer.Serialize(title)}}, "body": {{body}}}""";

    private async Task<Post> SavePostAsync(bool publish)
    {
        var post = new PostFactory(api.Data).Build();
        var mine = Post.CreateDraft(_author, post.BlogId, post.Title, post.Body, api.Time.GetUtcNow());
        if (publish)
        {
            mine.Publish(api.Time.GetUtcNow());
        }

        await api.SaveAsync<PostsDbContext>(mine);
        return mine;
    }

    private async Task<Subsite> SaveTopicAsync()
    {
        var topic = new Subsite
        {
            Id = Guid.CreateVersion7(api.Time.GetUtcNow()),
            Type = SubsiteType.Topic,
            CreatedAt = api.Time.GetUtcNow(),
        };
        await api.SaveAsync<BlogsDbContext>(topic);
        return topic;
    }

    private async Task<Subsite> SavePersonalBlogAsync(Guid owner)
    {
        var blog = Subsite.CreatePersonal(owner, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(blog);
        return blog;
    }

    private async Task<int> CountAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PostsDbContext>().Posts.CountAsync(Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? content, Guid? account)
    {
        using var request = new HttpRequestMessage(method, path);
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static async Task<string[]> ErrorFieldsOf(HttpResponseMessage response) =>
        [.. (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").EnumerateObject()
            .Select(e => e.Name).Order(StringComparer.Ordinal)];
}
