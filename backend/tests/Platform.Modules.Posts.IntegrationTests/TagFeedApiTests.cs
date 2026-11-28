using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Modules.Blogs;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E11-08, ADR-0120: the feed of a hashtag <c>/api/v1/feeds/tags/{tag}</c> gives the published posts whose text carries
/// the tag, whatever its case, newest first, page by page with the cursor of the fresh feed (ADR-0043).
/// </summary>
public sealed class TagFeedApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Subsite _topic = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var now = api.Time.GetUtcNow();
        _author = Guid.CreateVersion7(now);
        _topic = Subsite.CreateTopic("Кино", $"cinema{_author.ToString("N")[..8]}", Guid.CreateVersion7(now), now);
        await api.SaveAsync<BlogsDbContext>(_topic);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnyoneReadsThePostsWithTheTagNewestFirstPageByPage()
    {
        var tagged = new List<Guid>();
        foreach (var text in new[] { "Смотрел #Кино", "Снова #КИНО", "И опять #кино, #театр" })
        {
            api.Time.Advance(TimeSpan.FromMinutes(1));
            tagged.Add(await SaveAsync(text, published: true));
        }

        var (first, cursor) = await PageAsync("кино", "?limit=2");
        var (last, end) = await PageAsync("кино", $"?limit=2&cursor={Uri.EscapeDataString(cursor!)}");

        Assert.Equal(tagged.AsEnumerable().Reverse(), [.. first, .. last]);
        Assert.Null(end);
    }

    [Theory]
    [InlineData("КиНо")]
    [InlineData("%23кино")]
    public async Task TagIsAskedForInAnyCaseWithOrWithoutItsHash(string asked)
    {
        var post = await SaveAsync("Про #кино", published: true);

        Assert.Equal([post], (await PageAsync(asked)).Items);
    }

    [Fact]
    public async Task DraftsDeletedPostsAndOtherTagsAreNotInIt()
    {
        var shown = await SaveAsync("Про #кино", published: true);
        await SaveAsync("Черновик про #кино", published: false);
        await SaveAsync("Удалённый про #кино", published: true, delete: true);
        var theatre = await SaveAsync("Про #театр и кино", published: true);

        Assert.Equal([shown], (await PageAsync("кино")).Items);
        Assert.Equal([theatre], (await PageAsync("театр")).Items);
    }

    [Fact]
    public async Task TagNoPostCarriesIsAnEmptyLastPage()
    {
        var (items, cursor) = await PageAsync("никто");

        Assert.Empty(items);
        Assert.Null(cursor);
    }

    [Theory]
    [InlineData("2026")]
    [InlineData("два%20слова")]
    public async Task WordThatIsNotATagIsAValidationError(string asked)
    {
        using var response = await _client.GetAsync($"/api/v1/feeds/tags/{asked}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await CodeOf(response));
    }

    private async Task<Guid> SaveAsync(string text, bool published, bool delete = false)
    {
        var post = Post.CreateDraft(_author, _topic.Id, "Заголовок", PostFactory.Body(text), api.Time.GetUtcNow());
        if (published)
        {
            post.Publish(api.Time.GetUtcNow());
        }

        if (delete)
        {
            post.Delete(api.Time.GetUtcNow());
        }

        await api.SaveAsync<PostsDbContext>(post);
        return post.Id;
    }

    private async Task<(List<Guid> Items, string? Cursor)> PageAsync(string tag, string query = "")
    {
        var page = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/feeds/tags/{tag}{query}", Ct);
        var next = page.GetProperty("nextCursor");
        return ([.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())],
            next.ValueKind == JsonValueKind.Null ? null : next.GetString());
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
