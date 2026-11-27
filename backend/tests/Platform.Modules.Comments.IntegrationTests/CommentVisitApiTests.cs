using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity;
using Platform.Modules.Posts;
using Platform.Testing.Api;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>
/// E09-08 (ADR-0109): <c>POST /api/v1/posts/{postId}/comments/visit</c> names the comments new to the signed-in reader
/// since the previous opening — others', not deleted, not of an author the reader blocked, in the order of the tree —
/// and moves the opening on. The first opening has nothing new; a guest is refused, a post that is not published is
/// not found.
/// </summary>
public sealed class CommentVisitApiTests(CommentsApi api) : IClassFixture<CommentsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _reader;
    private Guid _other;
    private Guid _blocked;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var tag = Guid.CreateVersion7().ToString("N")[..8];
        _reader = await SaveConfirmedAccountAsync(1, $"reader{tag}@example.com");
        _other = await SaveConfirmedAccountAsync(2, $"other{tag}@example.com");
        _blocked = await SaveConfirmedAccountAsync(3, $"blocked{tag}@example.com");
        using var blocked = await SendAsync(
            HttpMethod.Put, $"/api/v1/preferences/me/blocks/{_blocked}", content: null, _reader);
        Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task FirstOpeningHasNothingNewAndRemembersTheTime()
    {
        var post = await SavePostAsync();
        await CreateAsync(post, "Старый", parentId: null, _other);
        var opened = api.Time.GetUtcNow();

        var visit = await VisitAsync(post, _reader);

        Assert.Equal(JsonValueKind.Null, visit.GetProperty("previousVisitAt").ValueKind);
        Assert.Empty(visit.GetProperty("newIds").EnumerateArray());
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
        var row = await db.Visits.SingleAsync(v => v.AccountId == _reader && v.PostId == post, Ct);
        Assert.Equal(opened, row.VisitedAt);
    }

    [Fact]
    public async Task NextOpeningNamesOthersCommentsWrittenSinceInTheOrderOfTheTree()
    {
        var post = await SavePostAsync();
        var old = await CreateAsync(post, "Старый", parentId: null, _other);
        await VisitAsync(post, _reader);
        var opened = api.Time.GetUtcNow();
        api.Time.Advance(TimeSpan.FromMinutes(5));

        var laterRoot = await CreateAsync(post, "Новый в корне", parentId: null, _other);
        var replyToOld = await CreateAsync(post, "Новый ответ", old, _other);
        await CreateAsync(post, "Свой", old, _reader);
        await CreateAsync(post, "От заблокированного", parentId: null, _blocked);
        var deleted = await CreateAsync(post, "Удалю", parentId: null, _other);
        using var removed = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{deleted}", content: null, _other);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var visit = await VisitAsync(post, _reader);

        Assert.Equal(opened, visit.GetProperty("previousVisitAt").GetDateTimeOffset());
        Assert.Equal([replyToOld, laterRoot], IdsOf(visit));
    }

    [Fact]
    public async Task OpeningMovesOnSoTheSameCommentsAreNotNewTwice()
    {
        var post = await SavePostAsync();
        await VisitAsync(post, _reader);
        var fresh = await CreateAsync(post, "Новый", parentId: null, _other);

        Assert.Equal([fresh], IdsOf(await VisitAsync(post, _reader)));
        Assert.Empty(IdsOf(await VisitAsync(post, _reader)));
        // Another reader keeps its own opening.
        Assert.Empty(IdsOf(await VisitAsync(post, _other)));
    }

    [Fact]
    public async Task GuestIsRefusedAndAPostNotPublishedIsNotFound()
    {
        var post = await SavePostAsync();
        using var guest = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{post}/comments/visit", "{}", account: null);
        using var unknown = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/posts/{Guid.CreateVersion7(api.Time.GetUtcNow())}/comments/visit",
            "{}",
            _reader);

        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(
            CommentEndpoints.PostNotFoundCode,
            (await unknown.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    private async Task<JsonElement> VisitAsync(Guid postId, Guid account)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/posts/{postId}/comments/visit", "{}", account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Guid[] IdsOf(JsonElement visit) =>
        [.. visit.GetProperty("newIds").EnumerateArray().Select(id => id.GetGuid())];

    private async Task<Guid> CreateAsync(Guid postId, string text, Guid? parentId, Guid account)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/posts/{postId}/comments",
            JsonSerializer.Serialize(new { text, parentId }),
            account);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> SavePostAsync()
    {
        var now = api.Time.GetUtcNow();
        var post = Post.CreateDraft(
            Guid.CreateVersion7(now), Guid.CreateVersion7(now), "Пост", PostFactory.Body("Текст поста."), now);
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
}
