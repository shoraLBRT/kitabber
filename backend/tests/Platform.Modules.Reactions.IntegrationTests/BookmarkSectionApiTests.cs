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
/// E04-12: the section «Закладки» through <c>/api/v1/feeds/bookmarks</c> (posts) and <c>/api/v1/comments/bookmarks</c>
/// (comments) — only the signed-in person's own bookmarks, newest bookmark first, page by page, and deleted content in
/// its place without the content (ADR-0116).
/// </summary>
public sealed class BookmarkSectionApiTests(ReactionsApi api) : IClassFixture<ReactionsApi>, IAsyncLifetime
{
    private const string _postsPath = "/api/v1/feeds/bookmarks";
    private const string _commentsPath = "/api/v1/comments/bookmarks";

    private HttpClient _client = null!;
    private Guid _author;
    private Guid _reader;
    private Guid _other;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = await SaveAccountAsync(1);
        _reader = await SaveAccountAsync(2);
        _other = await SaveAccountAsync(3);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PostsAreInTheOrderTheyWereBookmarkedNewestFirst()
    {
        var older = await SavePostAsync("Старый");
        var newer = await SavePostAsync("Новый");
        // Bookmarked in the other order than written: the section follows the bookmarks.
        await BookmarkAsync($"/api/v1/posts/{newer.Id}/bookmark", _reader);
        await BookmarkAsync($"/api/v1/posts/{older.Id}/bookmark", _reader);

        var items = await ItemsAsync(_postsPath, _reader);

        Assert.Equal([older.Id, newer.Id], items.Select(i => i.GetProperty("postId").GetGuid()));
        Assert.Equal("Старый", items[0].GetProperty("card").GetProperty("post").GetProperty("title").GetString());
    }

    [Fact]
    public async Task CommentsAreInTheOrderTheyWereBookmarkedWithTheirPost()
    {
        var post = await SavePostAsync("Пост с обсуждением");
        var first = await SaveCommentAsync(post.Id, _author);
        var second = await SaveCommentAsync(post.Id, _other);
        await BookmarkAsync($"/api/v1/comments/{second.Id}/bookmark", _reader);
        await BookmarkAsync($"/api/v1/comments/{first.Id}/bookmark", _reader);

        var items = await ItemsAsync(_commentsPath, _reader);

        Assert.Equal([first.Id, second.Id], items.Select(i => i.GetProperty("commentId").GetGuid()));
        var comment = items[0].GetProperty("comment");
        Assert.Equal("Пост с обсуждением", comment.GetProperty("postTitle").GetString());
        Assert.Equal(post.Id, comment.GetProperty("postId").GetGuid());
    }

    [Fact]
    public async Task OnlyTheOwnersBookmarksAreListedAndAGuestHasNone()
    {
        var mine = await SavePostAsync("Мой выбор");
        var theirs = await SavePostAsync("Чужой выбор");
        var comment = await SaveCommentAsync(mine.Id, _author);
        await BookmarkAsync($"/api/v1/posts/{mine.Id}/bookmark", _reader);
        await BookmarkAsync($"/api/v1/posts/{theirs.Id}/bookmark", _other);
        await BookmarkAsync($"/api/v1/comments/{comment.Id}/bookmark", _other);

        Assert.Equal([mine.Id], (await ItemsAsync(_postsPath, _reader)).Select(i => i.GetProperty("postId").GetGuid()));
        Assert.Empty(await ItemsAsync(_commentsPath, _reader));
        Assert.Empty(await ItemsAsync(_postsPath, _author));

        using var guestPosts = await SendAsync(HttpMethod.Get, _postsPath, account: null);
        using var guestComments = await SendAsync(HttpMethod.Get, _commentsPath, account: null);
        Assert.Equal(HttpStatusCode.Unauthorized, guestPosts.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, guestComments.StatusCode);
    }

    [Fact]
    public async Task DeletedContentStaysInItsPlaceWithoutTheContent()
    {
        var kept = await SavePostAsync("Остался");
        var gone = await SavePostAsync("Удалён");
        var comment = await SaveCommentAsync(kept.Id, _author);
        var underGone = await SaveCommentAsync(gone.Id, _author);
        var shown = await SaveCommentAsync(kept.Id, _other);
        await BookmarkAsync($"/api/v1/posts/{kept.Id}/bookmark", _reader);
        await BookmarkAsync($"/api/v1/posts/{gone.Id}/bookmark", _reader);
        await BookmarkAsync($"/api/v1/comments/{comment.Id}/bookmark", _reader);
        await BookmarkAsync($"/api/v1/comments/{underGone.Id}/bookmark", _reader);
        await BookmarkAsync($"/api/v1/comments/{shown.Id}/bookmark", _reader);
        await DeletePostAsync(gone.Id);
        await DeleteCommentAsync(comment.Id);

        var posts = await ItemsAsync(_postsPath, _reader);
        var comments = await ItemsAsync(_commentsPath, _reader);

        Assert.Equal([gone.Id, kept.Id], posts.Select(i => i.GetProperty("postId").GetGuid()));
        Assert.Equal(JsonValueKind.Null, posts[0].GetProperty("card").ValueKind);
        Assert.Equal(JsonValueKind.Object, posts[1].GetProperty("card").ValueKind);
        Assert.Equal([shown.Id, underGone.Id, comment.Id], comments.Select(i => i.GetProperty("commentId").GetGuid()));
        Assert.Equal(
            [JsonValueKind.Object, JsonValueKind.Null, JsonValueKind.Null],
            comments.Select(i => i.GetProperty("comment").ValueKind));
    }

    [Fact]
    public async Task ThePagesFollowOneAnotherByTheCursor()
    {
        var posts = new List<Post>();
        for (var i = 0; i < 3; i++)
        {
            posts.Add(await SavePostAsync($"Пост {i}"));
        }

        foreach (var post in posts)
        {
            await BookmarkAsync($"/api/v1/posts/{post.Id}/bookmark", _reader);
        }

        using var first = await SendAsync(HttpMethod.Get, $"{_postsPath}?limit=2", _reader);
        var firstPage = await JsonOf(first);
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        Assert.NotNull(cursor);
        using var second = await SendAsync(HttpMethod.Get, $"{_postsPath}?limit=2&cursor={cursor}", _reader);
        var secondPage = await JsonOf(second);

        Assert.Equal(
            [posts[2].Id, posts[1].Id, posts[0].Id],
            firstPage.GetProperty("items").EnumerateArray()
                .Concat(secondPage.GetProperty("items").EnumerateArray())
                .Select(i => i.GetProperty("postId").GetGuid()));
        Assert.Equal(JsonValueKind.Null, secondPage.GetProperty("nextCursor").ValueKind);
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

    private async Task<Comment> SaveCommentAsync(Guid postId, Guid author)
    {
        var comment = Comment.CreateNative(author, postId, parent: null, "Комментарий", api.Time.GetUtcNow());
        await api.SaveAsync<CommentsDbContext>(comment);
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return comment;
    }

    private async Task DeletePostAsync(Guid postId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        var post = await db.Posts.SingleAsync(p => p.Id == postId, Ct);
        post.Delete(api.Time.GetUtcNow());
        await db.SaveChangesAsync(Ct);
    }

    private async Task DeleteCommentAsync(Guid commentId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
        var comment = await db.Comments.SingleAsync(c => c.Id == commentId, Ct);
        comment.Delete(api.Time.GetUtcNow());
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> SaveAccountAsync(int number)
    {
        var account = new Account
        {
            Id = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(number)),
            CreatedAt = api.Time.GetUtcNow(),
        };
        var email = $"section{number}@example.com";
        account.ConfirmEmail(email, email, api.Time.GetUtcNow());
        await api.SaveAsync<IdentityDbContext>(account);
        return account.Id;
    }

    // Each bookmark a second after the one before, so their order is the order they were put in.
    private async Task BookmarkAsync(string path, Guid account)
    {
        using var put = await SendAsync(HttpMethod.Put, path, account);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        api.Time.Advance(TimeSpan.FromSeconds(1));
    }

    private async Task<List<JsonElement>> ItemsAsync(string path, Guid account)
    {
        using var response = await SendAsync(HttpMethod.Get, path, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await JsonOf(response)).GetProperty("items").EnumerateArray()];
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
}
