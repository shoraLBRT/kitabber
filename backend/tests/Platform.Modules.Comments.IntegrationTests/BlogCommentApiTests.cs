using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Platform.Modules.Blogs;
using Platform.Modules.Identity;
using Platform.Modules.Posts;
using Platform.Testing.Api;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>
/// E09-15 (ADR-0114): the tab «Комментарии» through <c>/api/v1/comments/blogs/{blogId}</c> — a personal blog lists its
/// owner's comments, a topic every comment under its posts, newest first, page by page, each with its post; deleted
/// comments, unpublished posts and the authors the reader blocked are left out; an unknown blog is 404.
/// </summary>
public sealed class BlogCommentApiTests(CommentsApi api) : IClassFixture<CommentsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _owner;
    private Guid _other;
    private Guid _reader;
    private Subsite _blog = null!;
    private Subsite _topic = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var tag = Guid.CreateVersion7().ToString("N")[..8];
        _owner = await SaveConfirmedAccountAsync(1, $"owner{tag}@example.com");
        _other = await SaveConfirmedAccountAsync(2, $"other{tag}@example.com");
        _reader = await SaveConfirmedAccountAsync(3, $"reader{tag}@example.com");
        _blog = Subsite.CreatePersonal(_owner, api.Time.GetUtcNow());
        _topic = Subsite.CreateTopic("Игры", $"games{tag}", _other, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(_blog, _topic);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PersonalBlogListsItsOwnersCommentsNewestFirstWithTheirPosts()
    {
        var own = await SavePostAsync(_blog.Id, "Свой пост");
        var inTopic = await SavePostAsync(_topic.Id, "Пост в теме");
        var first = await CreateAsync(own.Id, "Первый", _owner);
        await CreateAsync(own.Id, "Чужой", _other);
        var second = await CreateAsync(inTopic.Id, "Второй", _owner);

        var page = await ReadAsync(_blog.Id, account: null);

        Assert.Equal([second, first], IdsOf(page));
        var newest = page.GetProperty("items")[0];
        Assert.Equal(inTopic.Id, newest.GetProperty("postId").GetGuid());
        Assert.Equal(_topic.Id, newest.GetProperty("postBlogId").GetGuid());
        Assert.Equal("Пост в теме", newest.GetProperty("postTitle").GetString());
        Assert.Equal("post-v-teme", newest.GetProperty("postSlug").GetString());
        Assert.Equal("native", newest.GetProperty("kind").GetString());
        Assert.Equal(_owner, newest.GetProperty("authorAccountId").GetGuid());
        Assert.Equal("Второй", newest.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task TopicListsEveryCommentUnderItsPostsGuestOnesToo()
    {
        var inTopic = await SavePostAsync(_topic.Id, "Пост в теме");
        var elsewhere = await SavePostAsync(_blog.Id, "Пост в блоге");
        var byOwner = await CreateAsync(inTopic.Id, "От владельца блога", _owner);
        await CreateAsync(elsewhere.Id, "Не в теме", _other);
        var byOther = await CreateAsync(inTopic.Id, "От другого", _other);
        var guest = Comment.CreateGuest(
            inTopic.Id, parent: null, new GuestAuthor("Иван из Telegram", null), new CommentSource("-100123", "42"),
            "Гость из канала", api.Time.GetUtcNow());
        await api.SaveAsync<CommentsDbContext>(guest);

        var page = await ReadAsync(_topic.Id, account: null);

        Assert.Equal([guest.Id, byOther, byOwner], IdsOf(page));
        var shown = page.GetProperty("items")[0];
        Assert.Equal("guest", shown.GetProperty("kind").GetString());
        Assert.Equal("Иван из Telegram", shown.GetProperty("guest").GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeletedCommentsAndCommentsUnderUnpublishedPostsAreLeftOut()
    {
        var kept = await SavePostAsync(_topic.Id, "Остался");
        var removed = await SavePostAsync(_topic.Id, "Удалён", deleted: true);
        var shown = await CreateAsync(kept.Id, "Виден", _owner);
        var deleted = await CreateAsync(kept.Id, "Удалю", _owner);
        await SaveCommentAsync(removed.Id, "Под удалённым постом", _owner);
        using (var response = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{deleted}", _owner))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal([shown], IdsOf(await ReadAsync(_blog.Id, account: null)));
        Assert.Equal([shown], IdsOf(await ReadAsync(_topic.Id, account: null)));
    }

    [Fact]
    public async Task ListIsReadPageByPageWithTheCursor()
    {
        var post = await SavePostAsync(_blog.Id, "Пост");
        var first = await CreateAsync(post.Id, "1", _owner);
        var second = await CreateAsync(post.Id, "2", _owner);
        var third = await CreateAsync(post.Id, "3", _owner);

        var page = await ReadAsync(_blog.Id, account: null, "?limit=2");
        Assert.Equal([third, second], IdsOf(page));
        var cursor = page.GetProperty("nextCursor").GetString();
        Assert.NotNull(cursor);

        var next = await ReadAsync(_blog.Id, account: null, $"?limit=2&cursor={cursor}");
        Assert.Equal([first], IdsOf(next));
        Assert.Equal(JsonValueKind.Null, next.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task PageSkipsTheLeftOutCommentsAndStaysFull()
    {
        var kept = await SavePostAsync(_blog.Id, "Пост");
        var removed = await SavePostAsync(_blog.Id, "Удалён", deleted: true);
        var older = await CreateAsync(kept.Id, "Старый", _owner);
        for (var n = 0; n < 4; n++)
        {
            await SaveCommentAsync(removed.Id, $"Под удалённым {n}", _owner);
        }

        var newer = await CreateAsync(kept.Id, "Новый", _owner);

        var page = await ReadAsync(_blog.Id, account: null, "?limit=2");

        Assert.Equal([newer, older], IdsOf(page));
    }

    [Fact]
    public async Task AuthorsTheReaderBlockedAreLeftOutOnlyForThatReader()
    {
        var post = await SavePostAsync(_topic.Id, "Пост");
        var byOwner = await CreateAsync(post.Id, "От владельца", _owner);
        var byOther = await CreateAsync(post.Id, "От другого", _other);
        using (var blocked = await SendAsync(HttpMethod.Put, $"/api/v1/preferences/me/blocks/{_owner}", _reader))
        {
            Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        }

        Assert.Equal([byOther], IdsOf(await ReadAsync(_topic.Id, _reader)));
        Assert.Empty(IdsOf(await ReadAsync(_blog.Id, _reader)));
        Assert.Equal([byOther, byOwner], IdsOf(await ReadAsync(_topic.Id, _other)));
        Assert.Equal([byOwner], IdsOf(await ReadAsync(_blog.Id, account: null)));
    }

    [Fact]
    public async Task BlogWithoutCommentsGivesAnEmptyPage()
    {
        await SavePostAsync(_topic.Id, "Пост без комментариев");

        Assert.Empty(IdsOf(await ReadAsync(_blog.Id, account: null)));
        Assert.Empty(IdsOf(await ReadAsync(_topic.Id, account: null)));
    }

    [Fact]
    public async Task UnknownBlogIsNotFound()
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/comments/blogs/{Guid.CreateVersion7()}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(BlogCommentEndpoints.BlogNotFoundCode, problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?cursor=not-a-cursor")]
    public async Task WrongPageIsABadRequest(string query)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/comments/blogs/{_blog.Id}{query}", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<JsonElement> ReadAsync(Guid blogId, Guid? account, string query = "")
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/comments/blogs/{blogId}{query}", account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Guid[] IdsOf(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid())];

    private async Task<Guid> CreateAsync(Guid postId, string text, Guid account)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/posts/{postId}/comments")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { text, parentId = (Guid?)null }), Encoding.UTF8, "application/json"),
        };
        request.As(account.ToString("D"));
        using var response = await _client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    // A comment written straight to the database: one under a deleted post cannot be written through the API.
    private async Task SaveCommentAsync(Guid postId, string text, Guid account)
    {
        await api.SaveAsync<CommentsDbContext>(
            Comment.CreateNative(account, postId, parent: null, text, api.Time.GetUtcNow()));
        api.Time.Advance(TimeSpan.FromSeconds(1));
    }

    private async Task<Post> SavePostAsync(Guid blogId, string title, bool deleted = false)
    {
        var now = api.Time.GetUtcNow();
        var post = Post.CreateDraft(_owner, blogId, title, PostFactory.Body("Текст поста."), now);
        post.Publish(now);
        if (deleted)
        {
            post.Delete(now);
        }
        await api.SaveAsync<PostsDbContext>(post);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return post;
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

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Guid? account)
    {
        using var request = new HttpRequestMessage(method, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await _client.SendAsync(request, Ct);
    }
}
