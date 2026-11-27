using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Comments;
using Platform.Modules.Identity;
using Platform.Modules.Posts;
using Platform.Testing.Api;

namespace Platform.Modules.Reactions.IntegrationTests;

/// <summary>
/// E10-07: bookmarks on posts and comments through <c>/api/v1/posts/{postId}/bookmark</c>,
/// <c>/api/v1/comments/{id}/bookmark</c>, <c>/api/v1/posts/{postId}/bookmarks</c> and <c>/api/v1/bookmarks/posts</c> —
/// put and taken back, idempotent, private to the account, with the count shown only to the author (ADR-0113).
/// </summary>
public sealed class BookmarkApiTests(ReactionsApi api) : IClassFixture<ReactionsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _reader;
    private Guid _other;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = await SaveAccountAsync(1, confirmed: true);
        _reader = await SaveAccountAsync(2, confirmed: true);
        _other = await SaveAccountAsync(3, confirmed: true);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReaderBookmarksAPostAndTakesItOutAndOnlyTheAuthorSeesTheCount()
    {
        var post = await SavePostAsync(publish: true);

        using var put = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/bookmark", _reader);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal((true, (int?)null), await StateOf(put));

        using var other = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/bookmark", _other);
        Assert.Equal((true, (int?)null), await StateOf(other));

        using var authorsView = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/bookmarks", _author);
        Assert.Equal((false, (int?)2), StateOf((await JsonOf(authorsView)).GetProperty("post")));

        using var taken = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/bookmark", _reader);
        Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
        Assert.Equal((false, (int?)null), await StateOf(taken));
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task RepeatedPutAndTakeChangeNothing()
    {
        var post = await SavePostAsync(publish: true);

        using var first = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/bookmark", _reader);
        using var again = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/bookmark", _reader);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(1, await CountAsync());

        using var taken = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/bookmark", _reader);
        using var takenAgain = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/bookmark", _reader);
        Assert.Equal(HttpStatusCode.OK, takenAgain.StatusCode);
        Assert.Equal((false, (int?)null), await StateOf(takenAgain));
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task SimultaneousPutsKeepOneBookmark()
    {
        var post = await SavePostAsync(publish: true);

        var puts = Enumerable.Range(0, 5)
            .Select(_ => SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/bookmark", _reader))
            .ToList();
        foreach (var response in await Task.WhenAll(puts))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
        }

        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task OwnContentAndGuestCommentsAreBookmarked()
    {
        var post = await SavePostAsync(publish: true);
        var guest = Comment.CreateGuest(
            post.Id, parent: null, new GuestAuthor("Иван из Telegram", null), new CommentSource("-100123", "42"),
            "Привет из канала", api.Time.GetUtcNow());
        await api.SaveAsync<CommentsDbContext>(guest);

        using var own = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/bookmark", _author);
        using var guestComment = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{guest.Id}/bookmark", _reader);

        Assert.Equal((true, (int?)1), await StateOf(own));
        Assert.Equal((true, (int?)null), await StateOf(guestComment));
    }

    [Fact]
    public async Task PostPageReadsTheOwnBookmarksAndTheAuthorsCountsOfItsComments()
    {
        var post = await SavePostAsync(publish: true);
        var readers = await SaveCommentAsync(post.Id, _reader);
        var others = await SaveCommentAsync(post.Id, _other);
        var untouched = await SaveCommentAsync(post.Id, _other);

        using var a = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{readers.Id}/bookmark", _other);
        using var b = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{readers.Id}/bookmark", _author);
        using var c = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{others.Id}/bookmark", _reader);

        using var page = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/bookmarks", _reader);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var view = await JsonOf(page);
        Assert.Equal((false, (int?)null), StateOf(view.GetProperty("post")));
        var comments = view.GetProperty("comments").EnumerateArray()
            .ToDictionary(m => m.GetProperty("id").GetGuid(), StateOf);
        // The reader's comment: two people keep it, and the reader wrote it; the other's: the reader keeps it.
        Assert.Equal((false, (int?)2), comments[readers.Id]);
        Assert.Equal((true, (int?)null), comments[others.Id]);
        Assert.False(comments.ContainsKey(untouched.Id));
    }

    [Fact]
    public async Task FeedReadsTheBookmarksOfItsPosts()
    {
        var kept = await SavePostAsync(publish: true);
        var authors = await SavePostAsync(publish: true);
        var untouched = await SavePostAsync(publish: true);
        using var a = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{kept.Id}/bookmark", _reader);
        using var b = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{authors.Id}/bookmark", _other);

        using var asReader = await SendAsync(
            HttpMethod.Get, $"/api/v1/bookmarks/posts?ids={kept.Id}&ids={authors.Id}&ids={untouched.Id}", _reader);
        using var asAuthor = await SendAsync(
            HttpMethod.Get, $"/api/v1/bookmarks/posts?ids={kept.Id}&ids={authors.Id}&ids={untouched.Id}", _author);

        Assert.Equal(HttpStatusCode.OK, asReader.StatusCode);
        var readers = await MarksOf(asReader);
        Assert.Equal([kept.Id], readers.Keys);
        Assert.Equal((true, (int?)null), readers[kept.Id]);
        var authorsMarks = await MarksOf(asAuthor);
        Assert.Equal((false, (int?)1), authorsMarks[kept.Id]);
        Assert.Equal((false, (int?)1), authorsMarks[authors.Id]);
        Assert.False(authorsMarks.ContainsKey(untouched.Id));
    }

    [Theory]
    [InlineData("ids=not-a-guid")]
    [InlineData("many")]
    public async Task AWrongFeedRequestIsAValidationError(string query)
    {
        var path = query == "many"
            ? "/api/v1/bookmarks/posts?" + string.Join(
                '&', Enumerable.Range(0, BookmarkEndpoints.MaxFeedPosts + 1).Select(_ => $"ids={Guid.NewGuid()}"))
            : $"/api/v1/bookmarks/posts?{query}";

        using var response = await SendAsync(HttpMethod.Get, path, _reader);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await JsonOf(response)).GetProperty("errors").TryGetProperty("ids", out _));
    }

    [Fact]
    public async Task DraftDeletedOrUnknownContentIsNotFound()
    {
        var draft = await SavePostAsync(publish: false);
        var post = await SavePostAsync(publish: true);
        var deleted = await SaveCommentAsync(post.Id, _author);
        await DeleteCommentAsync(deleted.Id);

        using var draftPut = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{draft.Id}/bookmark", _reader);
        using var draftPage = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{draft.Id}/bookmarks", _reader);
        using var unknown = await SendAsync(HttpMethod.Put, $"/api/v1/comments/{Guid.CreateVersion7()}/bookmark", _reader);
        using var stub = await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{deleted.Id}/bookmark", _reader);

        Assert.Equal(HttpStatusCode.NotFound, draftPut.StatusCode);
        Assert.Equal(BookmarkEndpoints.PostNotFoundCode, await CodeOf(draftPut));
        Assert.Equal(HttpStatusCode.NotFound, draftPage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(BookmarkEndpoints.CommentNotFoundCode, await CodeOf(unknown));
        Assert.Equal(HttpStatusCode.NotFound, stub.StatusCode);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task AGuestHasNoBookmarksAndAnUnconfirmedAccountHasThem()
    {
        var post = await SavePostAsync(publish: true);
        var unconfirmed = await SaveAccountAsync(4, confirmed: false);

        using var guestPut = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/bookmark", account: null);
        using var guestTake = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/bookmark", account: null);
        using var guestPage = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{post.Id}/bookmarks", account: null);
        using var guestFeed = await SendAsync(HttpMethod.Get, $"/api/v1/bookmarks/posts?ids={post.Id}", account: null);
        using var notConfirmed = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}/bookmark", unconfirmed);

        Assert.Equal(HttpStatusCode.Unauthorized, guestPut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, guestTake.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, guestPage.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, guestFeed.StatusCode);
        // A bookmark is private and reaches no one, so it needs no confirmed email (ADR-0113).
        Assert.Equal(HttpStatusCode.OK, notConfirmed.StatusCode);
        Assert.Equal(1, await CountAsync());
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

    private async Task<Guid> SaveAccountAsync(int number, bool confirmed)
    {
        var account = new Account
        {
            Id = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(number)),
            CreatedAt = api.Time.GetUtcNow(),
        };
        if (confirmed)
        {
            var email = $"bookmarks{number}@example.com";
            account.ConfirmEmail(email, email, api.Time.GetUtcNow());
        }

        await api.SaveAsync<IdentityDbContext>(account);
        return account.Id;
    }

    private async Task<int> CountAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReactionsDbContext>().Bookmarks.CountAsync(Ct);
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

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static async Task<(bool Bookmarked, int? Count)> StateOf(HttpResponseMessage response) =>
        StateOf(await JsonOf(response));

    private static (bool Bookmarked, int? Count) StateOf(JsonElement state) =>
        (state.GetProperty("bookmarked").GetBoolean(),
            state.GetProperty("count").ValueKind == JsonValueKind.Null ? null : state.GetProperty("count").GetInt32());

    private static async Task<Dictionary<Guid, (bool Bookmarked, int? Count)>> MarksOf(HttpResponseMessage response) =>
        (await JsonOf(response)).GetProperty("posts").EnumerateArray()
            .ToDictionary(m => m.GetProperty("id").GetGuid(), StateOf);

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
