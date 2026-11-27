using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Platform.Modules.Identity;
using Platform.Modules.Posts;
using Platform.Realtime;
using Platform.Testing.Api;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>
/// E09-09 (ADR-0112): a new comment reaches those reading its post as the realtime event <c>comments.posted</c> in the
/// topic <c>post:&lt;id&gt;</c>, with where it stands; a comment the API refuses sends nothing.
/// </summary>
public sealed class CommentRealtimeApiTests(CommentsApi api) : IClassFixture<CommentsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _reader;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        api.Events.Clear();
        _client = api.CreateClient();
        var tag = Guid.CreateVersion7().ToString("N")[..8];
        _author = await SaveConfirmedAccountAsync(1, $"author{tag}@example.com");
        _reader = await SaveConfirmedAccountAsync(2, $"reader{tag}@example.com");
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task EveryNewCommentIsSentToTheReadersOfItsPost()
    {
        var post = await SavePostAsync(_author);

        var root = await CreateAsync(post, "Первый", parentId: null, _reader);
        var reply = await CreateAsync(post, "Ответ", root, _author);

        var events = api.Events.Published;
        Assert.All(events, e =>
        {
            Assert.Equal(RealtimeTopic.Post(post), e.Topic);
            Assert.Equal("comments.posted", e.Type);
        });
        Assert.Equal(
            [new CommentPostedEvent(root, post, null), new CommentPostedEvent(reply, post, root)],
            events.Select(e => (CommentPostedEvent)e.Data!));
    }

    [Fact]
    public async Task TheEventReachesTheClientAsJsonWithTheCommentIds()
    {
        var post = await SavePostAsync(_author);

        var root = await CreateAsync(post, "Первый", parentId: null, _reader);

        var data = JsonSerializer.SerializeToElement(
            Assert.Single(api.Events.Published).Data, JsonSerializerOptions.Web);
        Assert.Equal(root, data.GetProperty("commentId").GetGuid());
        Assert.Equal(post, data.GetProperty("postId").GetGuid());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("parentId").ValueKind);
    }

    [Fact]
    public async Task ARefusedCommentSendsNothing()
    {
        var post = await SavePostAsync(_author);
        var draft = await SavePostAsync(_author, publish: false);
        using var blocked = await SendAsync(
            HttpMethod.Put, $"/api/v1/preferences/me/blocks/{_reader}", content: null, _author);
        Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);

        using var empty = await CommentAsync(post, "   ", parentId: null, _author);
        using var unpublished = await CommentAsync(draft, "Текст", parentId: null, _author);
        using var byBlocked = await CommentAsync(post, "Текст", parentId: null, _reader);
        using var byGuest = await SendAsync(
            HttpMethod.Post, $"/api/v1/posts/{post}/comments", JsonSerializer.Serialize(new { text = "Текст" }), null);

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unpublished.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byBlocked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, byGuest.StatusCode);
        Assert.Empty(api.Events.Published);
    }

    private async Task<Guid> CreateAsync(Guid postId, string text, Guid? parentId, Guid account)
    {
        using var response = await CommentAsync(postId, text, parentId, account);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> CommentAsync(Guid postId, string text, Guid? parentId, Guid account) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/posts/{postId}/comments",
            JsonSerializer.Serialize(new { text, parentId }),
            account);

    private async Task<Guid> SavePostAsync(Guid author, bool publish = true)
    {
        var now = api.Time.GetUtcNow();
        var post = Post.CreateDraft(author, Guid.CreateVersion7(now), "Пост", PostFactory.Body("Текст поста."), now);
        if (publish)
        {
            post.Publish(now);
        }

        await api.SaveAsync<PostsDbContext>(post);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return post.Id;
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
