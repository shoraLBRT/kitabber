using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Platform.Modules.Identity;
using Platform.Modules.Posts;
using Platform.Testing.Api;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>
/// E11-06 (ADR-0091): the black list in the comments. A reader who blocked an account gets its comments marked to be
/// shown folded, in the tree and in a branch; a guest and other readers get them as they are. A blocked account may not
/// reply to the comment of the one who blocked it nor comment under their post, while it still writes elsewhere.
/// </summary>
public sealed class CommentBlockApiTests(CommentsApi api) : IClassFixture<CommentsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _owner;
    private Guid _blocked;
    private Guid _other;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var tag = Guid.CreateVersion7().ToString("N")[..8];
        _owner = await SaveConfirmedAccountAsync(1, $"owner{tag}@example.com");
        _blocked = await SaveConfirmedAccountAsync(2, $"blocked{tag}@example.com");
        _other = await SaveConfirmedAccountAsync(3, $"other{tag}@example.com");
        using var blocked = await SendAsync(
            HttpMethod.Put, $"/api/v1/preferences/me/blocks/{_blocked}", content: null, _owner);
        Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task BlockedAuthorsCommentsComeFoldedOnlyForTheReaderWhoBlockedThem()
    {
        var post = await SavePostAsync(author: _other);
        var byOther = await CreateAsync(post, "Обычный", parentId: null, _other);
        var byBlocked = await CreateAsync(post, "От заблокированного", byOther, _blocked);

        var forOwner = await FoldedAsync($"/api/v1/posts/{post}/comments", _owner);
        var branchForOwner = await FoldedAsync($"/api/v1/comments/{byOther}/branch", _owner);
        var forGuest = await FoldedAsync($"/api/v1/posts/{post}/comments", account: null);
        var forOther = await FoldedAsync($"/api/v1/posts/{post}/comments", _other);

        Assert.Equal([(byOther, false), (byBlocked, true)], forOwner);
        Assert.Equal([(byOther, false), (byBlocked, true)], branchForOwner);
        Assert.Equal([(byOther, false), (byBlocked, false)], forGuest);
        Assert.Equal([(byOther, false), (byBlocked, false)], forOther);
    }

    [Fact]
    public async Task BlockedAccountMayNotReplyToTheCommentOfTheOneWhoBlockedIt()
    {
        var post = await SavePostAsync(author: _other);
        var byOwner = await CreateAsync(post, "Мой комментарий", parentId: null, _owner);

        using var refused = await CommentAsync(post, "Ответ", byOwner, _blocked);
        using var allowed = await CommentAsync(post, "Ответ", byOwner, _other);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(CommentEndpoints.BlockedCode, await CodeOf(refused));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact]
    public async Task BlockedAccountMayNotCommentUnderThePostOfTheOneWhoBlockedItButWritesElsewhere()
    {
        var ownersPost = await SavePostAsync(author: _owner);
        var otherPost = await SavePostAsync(author: _other);

        using var refused = await CommentAsync(ownersPost, "Комментарий", parentId: null, _blocked);
        using var elsewhere = await CommentAsync(otherPost, "Комментарий", parentId: null, _blocked);
        // The one who blocked is not blocked back: they still reply to the blocked account.
        var byBlocked = await CreateAsync(otherPost, "Ещё", parentId: null, _blocked);
        using var back = await CommentAsync(otherPost, "Ответ", byBlocked, _owner);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(CommentEndpoints.BlockedCode, await CodeOf(refused));
        Assert.Equal(HttpStatusCode.Created, elsewhere.StatusCode);
        Assert.Equal(HttpStatusCode.Created, back.StatusCode);
    }

    [Fact]
    public async Task UnblockedAccountWritesAgain()
    {
        var ownersPost = await SavePostAsync(author: _owner);
        using var unblocked = await SendAsync(
            HttpMethod.Delete, $"/api/v1/preferences/me/blocks/{_blocked}", content: null, _owner);

        using var created = await CommentAsync(ownersPost, "Снова можно", parentId: null, _blocked);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private async Task<(Guid Id, bool Folded)[]> FoldedAsync(string path, Guid? account)
    {
        using var response = await SendAsync(HttpMethod.Get, path, content: null, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tree = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return
        [
            .. tree.GetProperty("comments").EnumerateArray()
                .Select(c => (c.GetProperty("id").GetGuid(), c.GetProperty("authorBlocked").GetBoolean())),
        ];
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

    private async Task<Guid> SavePostAsync(Guid author)
    {
        var now = api.Time.GetUtcNow();
        var post = Post.CreateDraft(author, Guid.CreateVersion7(now), "Пост", PostFactory.Body("Текст поста."), now);
        post.Publish(now);
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

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
