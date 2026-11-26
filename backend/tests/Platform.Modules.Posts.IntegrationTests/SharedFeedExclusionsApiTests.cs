using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Platform.Http.Pagination;
using Platform.Modules.Blogs;
using Platform.Modules.Posts.Contracts;
using Platform.Testing.Api;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E11-06 (ADR-0091): the shared feeds — fresh and a blog's — are cached for everyone (ADR-0080), and a signed-in
/// reader's exclusions are applied after the cache: the reader does not see the posts of the authors they blocked,
/// while a guest and another reader reading the same cached page still do. The cursor is the cached page's, so the
/// next page follows without a repeat or a gap.
/// </summary>
public sealed class SharedFeedExclusionsApiTests(MyFeedApi api) : IClassFixture<MyFeedApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _reader;
    private Guid _other;
    private Guid _author;
    private Guid _blocked;
    private Subsite _topic = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var now = api.Time.GetUtcNow();
        _reader = Guid.CreateVersion7(now);
        _other = Guid.CreateVersion7(now.AddSeconds(1));
        _author = Guid.CreateVersion7(now.AddSeconds(2));
        _blocked = Guid.CreateVersion7(now.AddSeconds(3));
        _topic = Subsite.CreateTopic("Наука", $"block{_reader.ToString("N")[..8]}", _author, now);
        await api.SaveAsync<BlogsDbContext>(_topic);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("/api/v1/feeds/fresh")]
    [InlineData("/api/v1/feeds/blogs/{topic}")]
    public async Task BlockedAuthorsPostsLeaveTheReadersCopyOfTheCachedPageOnly(string feed)
    {
        var path = feed.Replace("{topic}", _topic.Id.ToString("D"), StringComparison.Ordinal);
        var older = await SaveAsync(_author);
        var byBlocked = await SaveAsync(_blocked);
        var newer = await SaveAsync(_author);
        api.Exclusions.Set(_reader, new FeedExclusions([], [], [_blocked]));

        // A guest fills the cache first; the reader then gets the same cached page without the blocked author.
        var (guest, _) = await PageAsync(path, account: null);
        var (reader, _) = await PageAsync(path, _reader);
        var (other, _) = await PageAsync(path, _other);

        Assert.Equal([newer, byBlocked, older], guest);
        Assert.Equal([newer, older], reader);
        Assert.Equal([newer, byBlocked, older], other);
    }

    [Fact]
    public async Task ShorterPageKeepsTheCursorOfTheWholePage()
    {
        var oldest = await SaveAsync(_author);
        await SaveAsync(_blocked);
        await SaveAsync(_blocked);
        api.Exclusions.Set(_reader, new FeedExclusions([], [], [_blocked]));

        var (first, cursor) = await PageAsync("/api/v1/feeds/fresh?limit=2", _reader);
        var (last, end) = await PageAsync(
            $"/api/v1/feeds/fresh?limit=2&cursor={Uri.EscapeDataString(cursor!)}", _reader);

        Assert.Empty(first);
        Assert.NotNull(cursor);
        Assert.Equal([oldest], last);
        Assert.Null(end);
    }

    [Fact]
    public void BlockedCommentersBestCommentLeavesTheCardButThePostStays()
    {
        var (post, commenter, guest) = (Card(_author, _blocked), Card(_author, _other), Card(_author, commenter: null));
        var page = new CursorPage<FeedPostView>([post, commenter, guest], "next");

        var shown = ReaderExclusions.Apply(page, new FeedExclusions([], [], [_blocked]));

        Assert.Equal(3, shown.Items.Count);
        Assert.Null(shown.Items[0].BestComment);
        Assert.NotNull(shown.Items[1].BestComment);
        Assert.NotNull(shown.Items[2].BestComment);
        Assert.Equal("next", shown.NextCursor);
    }

    [Fact]
    public void HiddenPostsAndHiddenBlogsLeaveThePageToo()
    {
        var (a, b, c) = (Card(_author, null), Card(_author, null), Card(_author, null, blog: _topic.Id));
        var page = new CursorPage<FeedPostView>([a, b, c], null);

        var shown = ReaderExclusions.Apply(page, new FeedExclusions([a.Post.Id], [_topic.Id], []));

        Assert.Equal([b.Post.Id], shown.Items.Select(i => i.Post.Id));
        Assert.Same(page, ReaderExclusions.Apply(page, FeedExclusions.None));
    }

    private static FeedPostView Card(Guid author, Guid? commenter, Guid? blog = null)
    {
        var now = DateTimeOffset.UnixEpoch;
        var post = new PostView(
            Guid.CreateVersion7(), blog ?? Guid.CreateVersion7(), author, "Заголовок", null, new JsonObject(),
            "published", "native", null, now, now, now, new Dictionary<Guid, IReadOnlyList<Media.Contracts.ImageSource>>(),
            new Dictionary<Guid, Media.Contracts.ImagePreview>(), 0);
        var comment = new FeedCommentView(
            Guid.CreateVersion7(), null, commenter is null ? "Гость" : null, "Текст", commenter);
        return new FeedPostView(post, null, null, comment);
    }

    // Every post is published a minute after the one before, so the order of saving is the order of the feed.
    private async Task<Guid> SaveAsync(Guid author)
    {
        api.Time.Advance(TimeSpan.FromMinutes(1));
        var post = Post.CreateDraft(author, _topic.Id, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        post.Publish(api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(post);
        return post.Id;
    }

    private async Task<(List<Guid> Items, string? Cursor)> PageAsync(string path, Guid? account)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        using var response = await _client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var next = page.GetProperty("nextCursor");
        return ([.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())],
            next.ValueKind == JsonValueKind.Null ? null : next.GetString());
    }
}
