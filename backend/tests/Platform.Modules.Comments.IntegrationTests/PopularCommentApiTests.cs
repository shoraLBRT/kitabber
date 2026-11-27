using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Platform.Modules.Identity;
using Platform.Modules.Posts;
using Platform.Modules.Reactions;
using Platform.Testing.Api;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>
/// E08-12 (ADR-0105): the widget «Популярные комментарии» through <c>/api/v1/comments/popular</c> — the highest rated
/// native comments of the last day, never a guest comment from Telegram, given from the cache for a minute, without the
/// authors the reader blocked, and a wrong number asked is a validation error.
/// </summary>
public sealed class PopularCommentApiTests(CommentsApi api) : IClassFixture<CommentsApi>, IAsyncLifetime
{
    private const string Path = "/api/v1/comments/popular";

    private HttpClient _client = null!;
    private Guid _author;
    private Guid _otherAuthor;
    private Guid _fan;
    private Guid _otherFan;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var tag = Guid.CreateVersion7().ToString("N")[..8];
        _author = await SaveConfirmedAccountAsync(1, $"author{tag}@example.com");
        _otherAuthor = await SaveConfirmedAccountAsync(2, $"other{tag}@example.com");
        _fan = await SaveConfirmedAccountAsync(3, $"fan{tag}@example.com");
        _otherFan = await SaveConfirmedAccountAsync(4, $"fan2{tag}@example.com");
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WidgetShowsTheHighestRatedNativeCommentsOfTheDayWithTheirPost()
    {
        var post = await SavePostAsync("Новости дня");
        var liked = await CreateAsync(post.Id, "Отличный разбор", _author);
        var best = await CreateAsync(post.Id, "Лучший комментарий", _otherAuthor);
        var unrated = await CreateAsync(post.Id, "Без реакций", _author);
        var guest = Comment.CreateGuest(
            post.Id, parent: null, new GuestAuthor("Иван из Telegram", null), new CommentSource("-100123", "42"),
            "Гость из канала", api.Time.GetUtcNow());
        await api.SaveAsync<CommentsDbContext>(guest);
        // A guest comment takes no reactions through the API; a rating kept for it anyway still does not bring it in.
        await api.SaveAsync<ReactionsDbContext>(
            new Rating { TargetKind = ReactionTarget.Comment, TargetId = guest.Id, PostId = post.Id, Value = 9 });
        await ReactAsync(liked, _fan);
        await ReactAsync(best, _fan);
        await ReactAsync(best, _otherFan);

        var widget = await ReadAsync(account: null);

        Assert.Equal([best, liked], IdsOf(widget));
        Assert.DoesNotContain(unrated, IdsOf(widget));
        var first = widget[0];
        Assert.Equal(post.Id, first.GetProperty("postId").GetGuid());
        Assert.Equal(post.BlogId, first.GetProperty("postBlogId").GetGuid());
        Assert.Equal("Новости дня", first.GetProperty("postTitle").GetString());
        Assert.Equal("novosti-dnya", first.GetProperty("postSlug").GetString());
        Assert.Equal(_otherAuthor, first.GetProperty("authorAccountId").GetGuid());
        Assert.Equal("Лучший комментарий", first.GetProperty("text").GetString());
        Assert.Equal(2, first.GetProperty("rating").GetInt32());
    }

    [Fact]
    public async Task WidgetLeavesOutTheCommentsOlderThanADay()
    {
        var post = await SavePostAsync("Пост");
        var old = await CreateAsync(post.Id, "Вчерашний", _author);
        await ReactAsync(old, _fan);
        await ReactAsync(old, _otherFan);
        api.Time.Advance(PopularComments.Window);
        var fresh = await CreateAsync(post.Id, "Сегодняшний", _author);
        await ReactAsync(fresh, _fan);

        Assert.Equal([fresh], IdsOf(await ReadAsync(account: null)));
    }

    [Fact]
    public async Task WidgetIsGivenFromTheCacheForItsLifetime()
    {
        var post = await SavePostAsync("Пост");
        var first = await CreateAsync(post.Id, "Первый", _author);
        await ReactAsync(first, _fan);
        Assert.Equal([first], IdsOf(await ReadAsync(account: null)));

        var second = await CreateAsync(post.Id, "Второй", _otherAuthor);
        await ReactAsync(second, _fan);
        await ReactAsync(second, _otherFan);
        Assert.Equal([first], IdsOf(await ReadAsync(account: _fan)));

        api.Time.Advance(PopularCommentsCache.Lifetime);
        Assert.Equal([second, first], IdsOf(await ReadAsync(account: null)));
    }

    [Fact]
    public async Task WidgetLeavesOutTheAuthorsTheReaderBlockedOnlyForThatReader()
    {
        var post = await SavePostAsync("Пост");
        var byAuthor = await CreateAsync(post.Id, "От автора", _author);
        var byOther = await CreateAsync(post.Id, "От другого", _otherAuthor);
        await ReactAsync(byAuthor, _fan);
        await ReactAsync(byOther, _otherFan);
        using var blocked = await SendAsync(
            HttpMethod.Put, $"/api/v1/preferences/me/blocks/{_author}", content: null, _fan);
        Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);

        Assert.Equal([byOther], IdsOf(await ReadAsync(account: _fan)));
        Assert.Equal([byAuthor, byOther], IdsOf(await ReadAsync(account: _otherFan)));
        Assert.Equal([byAuthor, byOther], IdsOf(await ReadAsync(account: null)));
    }

    [Fact]
    public async Task WidgetGivesAtMostTheAskedNumber()
    {
        var post = await SavePostAsync("Пост");
        var first = await CreateAsync(post.Id, "Первый", _author);
        var second = await CreateAsync(post.Id, "Второй", _author);
        await ReactAsync(first, _fan);
        await ReactAsync(second, _fan);

        Assert.Equal([first], IdsOf(await ReadAsync(account: null, query: "?limit=1")));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("11")]
    public async Task WrongNumberIsAValidationError(string limit)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Path}?limit={limit}", content: null, account: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("limit", out _));
    }

    [Fact]
    public async Task WidgetWithoutRatedCommentsIsEmpty()
    {
        var post = await SavePostAsync("Пост");
        await CreateAsync(post.Id, "Без реакций", _author);

        Assert.Empty(await ReadAsync(account: null));
    }

    private async Task<JsonElement[]> ReadAsync(Guid? account, string query = "")
    {
        using var response = await SendAsync(HttpMethod.Get, Path + query, content: null, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var widget = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return [.. widget.GetProperty("comments").EnumerateArray()];
    }

    private static Guid[] IdsOf(JsonElement[] comments) => [.. comments.Select(c => c.GetProperty("id").GetGuid())];

    private async Task ReactAsync(Guid commentId, Guid account)
    {
        using var response = await SendAsync(
            HttpMethod.Put, $"/api/v1/comments/{commentId}/reaction", """{"kind":"heart"}""", account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> CreateAsync(Guid postId, string text, Guid account)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/posts/{postId}/comments",
            JsonSerializer.Serialize(new { text, parentId = (Guid?)null }),
            account);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<Post> SavePostAsync(string title)
    {
        var now = api.Time.GetUtcNow();
        var post = Post.CreateDraft(_author, Guid.CreateVersion7(now), title, PostFactory.Body("Текст поста."), now);
        post.Publish(now);
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
}
