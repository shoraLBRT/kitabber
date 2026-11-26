using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Modules.Blogs;
using Platform.Modules.Identity;
using Platform.Modules.Posts.Contracts;
using Platform.Testing.Api;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E05-09: <c>POST /api/v1/posts/{id}/publish</c> with its checks (ADR-0046) — an empty post, the length of the title,
/// the size of the post and of a block — the confirmed email it needs and the rights of the author.
/// </summary>
public sealed class PublishApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _stranger;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = await SaveConfirmedAccountAsync(1, "author@example.com");
        _stranger = await SaveConfirmedAccountAsync(2, "stranger@example.com");
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AuthorPublishesAShortPostWithoutATitle()
    {
        var id = await CreateAsync(Body(Paragraph("Короткий пост")), title: null);

        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{id}/publish", null, _author);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var post = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("published", post.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, post.GetProperty("title").ValueKind);
        Assert.Equal(api.Time.GetUtcNow(), post.GetProperty("publishedAt").GetDateTimeOffset());

        using var read = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{id}", null, account: null);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    // E14-01, ADR-0094: publishing, editing a published post and deleting it each announce PostChanged, so search
    // follows; editing a draft announces nothing.
    [Fact]
    public async Task PublishEditAndDeleteAnnounceThePostChange()
    {
        var events = new System.Collections.Concurrent.ConcurrentQueue<PostChanged>();
        var endpoint = api.Services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            $"posts-tests-post-changed-{Guid.NewGuid():N}",
            (_, e) => e.Handler<PostChanged>(c =>
            {
                events.Enqueue(c.Message);
                return Task.CompletedTask;
            }));
        await endpoint.Ready;
        try
        {
            var id = await CreateAsync(Body(Paragraph("Черновик")), title: "Черновик");
            (await SendAsync(HttpMethod.Put, $"/api/v1/posts/{id}", Input(Body(Paragraph("Ещё черновик")), "Черновик"),
                _author)).Dispose();
            (await SendAsync(HttpMethod.Post, $"/api/v1/posts/{id}/publish", null, _author)).Dispose();
            await WaitForAsync(events, id, 1);
            (await SendAsync(HttpMethod.Put, $"/api/v1/posts/{id}", Input(Body(Paragraph("Правка")), "Пост"),
                _author)).Dispose();
            await WaitForAsync(events, id, 2);
            using var deleted = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{id}", null, _author);
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            await WaitForAsync(events, id, 3);

            await Task.Delay(500, Ct);
            Assert.Equal(3, events.Count(e => e.PostId == id));
        }
        finally
        {
            await endpoint.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitForAsync(IEnumerable<PostChanged> events, Guid id, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (events.Count(e => e.PostId == id) < count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"PostChanged #{count} was not published in 30s");
            await Task.Delay(100, Ct);
        }
    }

    [Fact]
    public async Task AuthorPublishesInATopic()
    {
        var topic = Subsite.CreateTopic("Игры", "games", Guid.CreateVersion7(api.Time.GetUtcNow()), api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        var id = await CreateAsync(Body(Paragraph("Пост в теме")), title: "Про игры", topic.Id);

        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{id}/publish", null, _author);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var post = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(topic.Id, post.GetProperty("blogId").GetGuid());
        Assert.Equal("pro-igry", post.GetProperty("slug").GetString());
    }

    // E11-01, ADR-0082: a post published in a topic is in the topic's feed, and a draft is not.
    [Fact]
    public async Task PostPublishedInATopicIsInItsFeed()
    {
        var topic = Subsite.CreateTopic("Игры", $"games{_author.ToString("N")[..8]}", _stranger, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        var published = await CreateAsync(Body(Paragraph("Пост в теме")), title: "Про игры", topic.Id);
        await CreateAsync(Body(Paragraph("Черновик в теме")), title: "Черновик", topic.Id);
        await CreateAsync(Body(Paragraph("Пост в своём блоге")), title: "Своё");
        (await SendAsync(HttpMethod.Post, $"/api/v1/posts/{published}/publish", null, _author)).Dispose();

        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/feeds/blogs/{topic.Id}", null, account: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items");
        Assert.Equal([published], items.EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid()));
        Assert.Equal(topic.Id, items[0].GetProperty("topic").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task PublishingAgainKeepsThePostAsItIs()
    {
        var id = await CreateAsync(Body(Paragraph("Текст")), title: null);
        (await SendAsync(HttpMethod.Post, $"/api/v1/posts/{id}/publish", null, _author)).Dispose();
        var publishedAt = api.Time.GetUtcNow();
        api.Time.Advance(TimeSpan.FromMinutes(5));

        using var again = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{id}/publish", null, _author);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(publishedAt, (await again.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("publishedAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"id": "d1", "type": "delimiter", "data": {}}]""")]
    [InlineData("""[{"id": "p1", "type": "paragraph", "data": {"text": " <b></b>&nbsp;<br>"}}]""")]
    public async Task EmptyPostIsNotPublished(string blocks)
    {
        var id = await CreateAsync($$"""{"version": 1, "blocks": {{blocks}}}""", title: "Только заголовок");

        using var response = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{id}/publish", null, _author);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal(PostChecks.EmptyMessage, problem.GetProperty("errors").GetProperty("body")[0].GetString());
        Assert.Equal("draft", await StatusAsync(id));
    }

    [Fact]
    public async Task TitleLongerThanTheLimitIsNotPublished()
    {
        var fits = await CreateAsync(Body(Paragraph("Текст")), new string('я', PostChecks.TitleMaxLength));
        var tooLong = await CreateAsync(Body(Paragraph("Текст")), new string('я', PostChecks.TitleMaxLength + 1));

        using var fitting = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{fits}/publish", null, _author);
        using var refused = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{tooLong}/publish", null, _author);

        Assert.Equal(HttpStatusCode.OK, fitting.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(["title"], await ErrorFieldsOf(refused));
    }

    [Fact]
    public async Task BlockOrPostOverTheSizeLimitIsNotPublished()
    {
        var bigBlock = await CreateAsync(Body(Paragraph("Текст"), Paragraph(new string('a', PostChecks.BlockMaxBytes), "p2")), null);
        var paragraphs = Enumerable.Range(0, (PostChecks.PostMaxBytes / (PostChecks.BlockMaxBytes / 2)) + 1)
            .Select(i => Paragraph(new string('a', PostChecks.BlockMaxBytes / 2), $"p{i}"));
        var bigPost = await CreateAsync(Body([.. paragraphs]), null);

        using var block = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{bigBlock}/publish", null, _author);
        using var post = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{bigPost}/publish", null, _author);

        Assert.Equal(HttpStatusCode.BadRequest, block.StatusCode);
        Assert.Equal(["body.blocks[1]"], await ErrorFieldsOf(block));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        Assert.Equal(["body"], await ErrorFieldsOf(post));
    }

    [Fact]
    public async Task EditOfAPublishedPostPassesTheSameChecks()
    {
        var id = await CreateAsync(Body(Paragraph("Текст")), title: null);
        (await SendAsync(HttpMethod.Post, $"/api/v1/posts/{id}/publish", null, _author)).Dispose();

        using var emptied = await SendAsync(
            HttpMethod.Put, $"/api/v1/posts/{id}", Input("""{"version": 1, "blocks": []}""", null), _author);
        using var draftEdit = await SendAsync(
            HttpMethod.Put, $"/api/v1/posts/{await CreateAsync(Body(Paragraph("Черновик")), null)}",
            Input("""{"version": 1, "blocks": []}""", null), _author);

        Assert.Equal(HttpStatusCode.BadRequest, emptied.StatusCode);
        Assert.Equal(["body"], await ErrorFieldsOf(emptied));
        // A draft is saved as it is: writing is not lost to a check.
        Assert.Equal(HttpStatusCode.OK, draftEdit.StatusCode);
    }

    [Fact]
    public async Task OnlyTheAuthorWithAConfirmedEmailPublishes()
    {
        var id = await CreateAsync(Body(Paragraph("Текст")), title: null);
        var unconfirmed = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(3));
        var path = $"/api/v1/posts/{id}/publish";

        using var anonymous = await SendAsync(HttpMethod.Post, path, null, account: null);
        using var withoutEmail = await SendAsync(HttpMethod.Post, path, null, unconfirmed);
        using var stranger = await SendAsync(HttpMethod.Post, path, null, _stranger);
        using var missing = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{Guid.NewGuid()}/publish", null, _author);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutEmail.StatusCode);
        Assert.Equal(AccountPolicies.EmailNotConfirmedCode, await CodeOf(withoutEmail));
        // Someone else's draft is not found, as on reading.
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("draft", await StatusAsync(id));
    }

    private async Task<Guid> SaveConfirmedAccountAsync(int number, string email)
    {
        var account = new Account
        {
            Id = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(number)),
            CreatedAt = api.Time.GetUtcNow(),
        };
        account.ConfirmEmail(email, email, api.Time.GetUtcNow());
        await api.SaveAsync<IdentityDbContext>(account);
        return account.Id;
    }

    private async Task<Guid> CreateAsync(string body, string? title, Guid? blogId = null)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/v1/posts", Input(body, title, blogId), _author);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<string?> StatusAsync(Guid id)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{id}", null, _author);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("status").GetString();
    }

    private static string Paragraph(string text, string id = "p1") =>
        $$$"""{"id": "{{{id}}}", "type": "paragraph", "data": {"text": {{{JsonSerializer.Serialize(text)}}}}}""";

    private static string Body(params string[] blocks) =>
        $$"""{"version": 1, "blocks": [{{string.Join(", ", blocks)}}]}""";

    private static string Input(string body, string? title, Guid? blogId = null) =>
        $$"""{"blogId": {{(blogId is { } id ? $"\"{id}\"" : "null")}}, "title": {{JsonSerializer.Serialize(title)}}, "body": {{body}}}""";

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
