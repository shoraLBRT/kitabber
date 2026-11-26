using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Modules.Comments;
using Platform.Modules.Identity;
using Platform.Modules.Posts;
using Platform.Realtime;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Reactions.IntegrationTests;

/// <summary>The Api host on PostgreSQL and Redis in Testcontainers, signed in by the test scheme.</summary>
public sealed class ReactionsApi : IntegrationApiFactory
{
    public ReactionsApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(services =>
        {
            services.AddTestAuthentication();
            services.RemoveAll<IRealtimePublisher>();
            services.AddSingleton<IRealtimePublisher>(Events);
        });

    /// <summary>What the module published to the realtime channel (ADR-0072).</summary>
    public RecordingPublisher Events { get; } = new();
}

/// <summary>Keeps every published event instead of sending it.</summary>
public sealed class RecordingPublisher : IRealtimePublisher
{
    private readonly ConcurrentQueue<(RealtimeTopic Topic, string Type, object? Data)> _events = new();

    public IReadOnlyList<(RealtimeTopic Topic, string Type, object? Data)> Published => [.. _events];

    public void Clear() => _events.Clear();

    public Task PublishAsync(RealtimeTopic topic, string type, object? data, CancellationToken cancellationToken = default)
    {
        _events.Enqueue((topic, type, data));
        return Task.CompletedTask;
    }
}

/// <summary>
/// E10-01: likes on posts and comments through <c>/api/v1/posts/{postId}/like</c>, <c>/api/v1/comments/{id}/like</c>
/// and <c>/api/v1/posts/{postId}/likes</c> — put and taken back, idempotent, never on one's own content or a guest
/// comment, and only by a signed-in account with a confirmed email.
/// </summary>
public sealed class LikeApiTests(ReactionsApi api) : IClassFixture<ReactionsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _reader;
    private Guid _other;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        api.Events.Clear();
        _client = api.CreateClient();
        _author = await SaveAccountAsync(1, "author@example.com", confirmed: true);
        _reader = await SaveAccountAsync(2, "reader@example.com", confirmed: true);
        _other = await SaveAccountAsync(3, "other@example.com", confirmed: true);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReaderLikesAPostAndTakesTheLikeBack()
    {
        var post = await SavePostAsync(publish: true);

        using var liked = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _reader);
        Assert.Equal(HttpStatusCode.OK, liked.StatusCode);
        Assert.Equal((1, true), await StateOf(liked));

        using var otherLiked = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _other);
        Assert.Equal((2, true), await StateOf(otherLiked));

        using var taken = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/like", _reader);
        Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        Assert.Equal((1, false), await StateOf(taken));
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task RepeatedLikeAndUnlikeChangeNothing()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await SaveCommentAsync(post.Id, _author);

        using var first = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _reader);
        using var again = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _reader);
        using var commentFirst = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{comment.Id}/like", _reader);
        using var commentAgain = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{comment.Id}/like", _reader);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal((1, true), await StateOf(again));
        Assert.Equal((1, true), await StateOf(commentAgain));
        Assert.Equal(2, await CountAsync());

        using var taken = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/like", _reader);
        using var takenAgain = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/like", _reader);
        using var neverLiked = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{comment.Id}/like", _other);

        Assert.Equal(HttpStatusCode.OK, takenAgain.StatusCode);
        Assert.Equal((0, false), await StateOf(takenAgain));
        Assert.Equal(HttpStatusCode.OK, neverLiked.StatusCode);
        Assert.Equal((1, false), await StateOf(neverLiked));
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task EveryChangeSendsTheNewCountsToTheReadersOfThePost()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await SaveCommentAsync(post.Id, _author);

        using var liked = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _reader);
        using var otherLiked = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _other);
        using var commentLiked = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{comment.Id}/like", _reader);
        using var commentTaken = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{comment.Id}/like", _reader);

        var events = api.Events.Published;
        Assert.All(events, e =>
        {
            Assert.Equal(RealtimeTopic.Post(post.Id), e.Topic);
            Assert.Equal("reactions.changed", e.Type);
        });
        Assert.Equal(
            [
                ("post", post.Id, "like:1"),
                ("post", post.Id, "like:2"),
                ("comment", comment.Id, "like:1"),
                ("comment", comment.Id, ""),
            ],
            events.Select(e => Shown((ReactionsChangedEvent)e.Data!)));
    }

    [Fact]
    public async Task ARepeatOrARefusalSendsNothing()
    {
        var post = await SavePostAsync(publish: true);

        using var first = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _reader);
        api.Events.Clear();
        using var again = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _reader);
        using var own = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _author);
        using var neverLiked = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/like", _other);

        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Empty(api.Events.Published);
    }

    private static (string Target, Guid TargetId, string Counts) Shown(ReactionsChangedEvent e) =>
        (e.Target, e.TargetId, string.Join(',', e.Reactions.Select(r => $"{r.Kind}:{r.Count}")));

    [Fact]
    public async Task OwnPostAndOwnCommentAreNotLiked()
    {
        var post = await SavePostAsync(publish: true);
        var comment = await SaveCommentAsync(post.Id, _reader);

        using var ownPost = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _author);
        using var ownComment = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{comment.Id}/like", _reader);

        Assert.Equal(HttpStatusCode.Forbidden, ownPost.StatusCode);
        Assert.Equal(LikeEndpoints.OwnContentCode, await CodeOf(ownPost));
        Assert.Equal(HttpStatusCode.Forbidden, ownComment.StatusCode);
        Assert.Equal(LikeEndpoints.OwnContentCode, await CodeOf(ownComment));
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task GuestCommentTakesNoLike()
    {
        var post = await SavePostAsync(publish: true);
        var guest = Comment.CreateGuest(
            post.Id, parent: null, new GuestAuthor("Иван из Telegram", null), new CommentSource("-100123", "42"),
            "Привет из канала", api.Time.GetUtcNow());
        await api.SaveAsync<CommentsDbContext>(guest);

        using var response = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{guest.Id}/like", _reader);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(LikeEndpoints.GuestCommentCode, await CodeOf(response));
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task PostPageReadsTheLikesOfThePostAndItsComments()
    {
        var post = await SavePostAsync(publish: true);
        var liked = await SaveCommentAsync(post.Id, _author);
        var quiet = await SaveCommentAsync(post.Id, _author);
        var otherPost = await SavePostAsync(publish: true);
        (await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _reader)).Dispose();
        (await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", _other)).Dispose();
        (await SendAsync(HttpMethod.Put, $"/api/v1/comments/{liked.Id}/like", _other)).Dispose();
        (await SendAsync(HttpMethod.Put, $"/api/v1/posts/{otherPost.Id}/like", _reader)).Dispose();

        using var asReader = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/likes", _reader);
        using var asGuest = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/likes", account: null);

        Assert.Equal(HttpStatusCode.OK, asReader.StatusCode);
        var reader = await asReader.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(post.Id, reader.GetProperty("postId").GetGuid());
        Assert.Equal(2, reader.GetProperty("post").GetProperty("count").GetInt32());
        Assert.True(reader.GetProperty("post").GetProperty("liked").GetBoolean());
        var comment = Assert.Single(reader.GetProperty("comments").EnumerateArray());
        Assert.Equal(liked.Id, comment.GetProperty("commentId").GetGuid());
        Assert.Equal(1, comment.GetProperty("count").GetInt32());
        Assert.False(comment.GetProperty("liked").GetBoolean());
        Assert.DoesNotContain(
            reader.GetProperty("comments").EnumerateArray(), c => c.GetProperty("commentId").GetGuid() == quiet.Id);

        var guest = await asGuest.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(2, guest.GetProperty("post").GetProperty("count").GetInt32());
        Assert.False(guest.GetProperty("post").GetProperty("liked").GetBoolean());
    }

    [Fact]
    public async Task DraftDeletedOrUnknownContentIsNotFound()
    {
        var draft = await SavePostAsync(publish: false);
        var post = await SavePostAsync(publish: true);
        var deleted = await SaveCommentAsync(post.Id, _author);
        await DeleteCommentAsync(deleted.Id);

        using var draftLike = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{draft.Id}/like", _reader);
        using var draftLikes = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{draft.Id}/likes", account: null);
        using var unknown = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{Guid.CreateVersion7()}/like", _reader);
        using var stub = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{deleted.Id}/like", _reader);

        Assert.Equal(HttpStatusCode.NotFound, draftLike.StatusCode);
        Assert.Equal(LikeEndpoints.PostNotFoundCode, await CodeOf(draftLike));
        Assert.Equal(HttpStatusCode.NotFound, draftLikes.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(LikeEndpoints.CommentNotFoundCode, await CodeOf(unknown));
        Assert.Equal(HttpStatusCode.NotFound, stub.StatusCode);
    }

    [Fact]
    public async Task OnlyASignedInAccountWithAConfirmedEmailLikes()
    {
        var post = await SavePostAsync(publish: true);
        var unconfirmed = await SaveAccountAsync(4, "new@example.com", confirmed: false);

        using var guest = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", account: null);
        using var guestUnlike = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/like", account: null);
        using var notConfirmed = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like", unconfirmed);

        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, guestUnlike.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notConfirmed.StatusCode);
        Assert.Equal(0, await CountAsync());
    }

    private async Task<Post> SavePostAsync(bool publish)
    {
        var now = api.Time.GetUtcNow();
        var post = Post.CreateDraft(_author, Guid.CreateVersion7(now), "Пост", PostFactory.Body("Текст поста."), now);
        if (publish)
        {
            post.Publish(now);
        }

        await api.SaveAsync<PostsDbContext>(post);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return post;
    }

    private async Task<Comment> SaveCommentAsync(Guid postId, Guid author)
    {
        var comment = Comment.CreateNative(author, postId, parent: null, "Комментарий", api.Time.GetUtcNow());
        await api.SaveAsync<CommentsDbContext>(comment);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return comment;
    }

    private async Task DeleteCommentAsync(Guid commentId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
        var comment = await db.Comments.SingleAsync(c => c.Id == commentId, Ct);
        comment.Delete(api.Time.GetUtcNow());
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> SaveAccountAsync(int number, string email, bool confirmed)
    {
        var account = new Account
        {
            Id = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(number)),
            CreatedAt = api.Time.GetUtcNow(),
        };
        if (confirmed)
        {
            account.ConfirmEmail(email, email, api.Time.GetUtcNow());
        }

        await api.SaveAsync<IdentityDbContext>(account);
        return account.Id;
    }

    private async Task<int> CountAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReactionsDbContext>().Reactions.CountAsync(Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Guid? account)
    {
        using var request = new HttpRequestMessage(method, path);
        if (method == HttpMethod.Put)
        {
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        }

        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<(int Count, bool Liked)> StateOf(HttpResponseMessage response)
    {
        var state = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (state.GetProperty("count").GetInt32(), state.GetProperty("liked").GetBoolean());
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
