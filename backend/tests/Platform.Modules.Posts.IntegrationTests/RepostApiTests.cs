using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Modules.Blogs;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Modules.Identity;
using Platform.Modules.Posts.Contracts;
using Platform.Testing.Api;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E10-08 (FR-REACT, ADR-0117): <c>/api/v1/posts/{id}/reposts</c> — a repost of someone else's post with a text is in
/// the reposter's blog and in the «Моя лента» of their subscribers with the original inside, the list of a post's
/// reposts, the rights and the checks, deleting the original hiding its reposts, and the event that tells the author.
/// </summary>
public sealed class RepostApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _reposter;
    private Guid _reader;
    private Subsite _authorBlog = null!;
    private Subsite _reposterBlog = null!;
    private Guid _original;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = await SaveConfirmedAccountAsync(1, "author@example.com");
        _reposter = await SaveConfirmedAccountAsync(2, "reposter@example.com");
        _reader = await SaveConfirmedAccountAsync(3, "reader@example.com");
        var now = api.Time.GetUtcNow();
        _authorBlog = Subsite.CreatePersonal(_author, now);
        _reposterBlog = Subsite.CreatePersonal(_reposter, now);
        await api.SaveAsync<BlogsDbContext>(_authorBlog, _reposterBlog);
        await api.SaveAsync<BlogsDbContext>(Subscription.Create(_reader, _reposterBlog.Id, now));
        _original = await SavePostAsync(_author, _authorBlog.Id);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RepostWithATextIsInTheReposterBlogAndInTheSubscribersFeedWithTheOriginal()
    {
        using var response = await RepostAsync(_original, "  Почитайте  ", _reposter);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var repost = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = repost.GetProperty("id").GetGuid();
        Assert.Equal(_original, repost.GetProperty("repostOfPostId").GetGuid());
        Assert.Equal("Почитайте", repost.GetProperty("repostText").GetString());
        Assert.Equal("published", repost.GetProperty("status").GetString());
        Assert.Equal(_reposterBlog.Id, repost.GetProperty("blogId").GetGuid());

        foreach (var path in new[] { $"/api/v1/feeds/blogs/{_reposterBlog.Id}", "/api/v1/feeds/my" })
        {
            var card = Assert.Single(await ItemsAsync(path, _reader));
            Assert.Equal(id, card.GetProperty("post").GetProperty("id").GetGuid());
            var original = card.GetProperty("original");
            Assert.Equal(_original, original.GetProperty("post").GetProperty("id").GetGuid());
            Assert.Equal(_authorBlog.Id, original.GetProperty("author").GetProperty("id").GetGuid());
            Assert.Equal(_reposterBlog.Id, card.GetProperty("author").GetProperty("id").GetGuid());
        }

        // The fresh feed has the original once and no repost of it (ADR-0117).
        Assert.Equal([_original], (await ItemsAsync("/api/v1/feeds/fresh", account: null))
            .Select(i => i.GetProperty("post").GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task ListHasTheRepostsOfAPostNewestFirstPageByPage()
    {
        var other = await SaveConfirmedAccountAsync(4, "other@example.com");
        (await RepostAsync(_original, "Первый", _reposter)).Dispose();
        api.Time.Advance(TimeSpan.FromMinutes(1));
        (await RepostAsync(_original, null, other)).Dispose();

        using var first = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{_original}/reposts?limit=1", null, null);
        var page = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var cursor = page.GetProperty("nextCursor").GetString();
        using var second = await SendAsync(
            HttpMethod.Get, $"/api/v1/posts/{_original}/reposts?limit=1&cursor={Uri.EscapeDataString(cursor!)}", null, null);
        var last = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);

        var newest = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(other, newest.GetProperty("authorAccountId").GetGuid());
        Assert.Equal(JsonValueKind.Null, newest.GetProperty("text").ValueKind);
        var oldest = Assert.Single(last.GetProperty("items").EnumerateArray());
        Assert.Equal(_reposter, oldest.GetProperty("authorAccountId").GetGuid());
        Assert.Equal("Первый", oldest.GetProperty("text").GetString());
        Assert.Equal(_reposterBlog.Id, oldest.GetProperty("author").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, last.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task DeletingTheOriginalHidesItsReposts()
    {
        using var created = await RepostAsync(_original, "Почитайте", _reposter);
        var repost = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var deleted = await SendAsync(HttpMethod.Delete, $"/api/v1/posts/{_original}", null, _author);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Empty(await ItemsAsync($"/api/v1/feeds/blogs/{_reposterBlog.Id}", _reader));
        Assert.Empty(await ItemsAsync("/api/v1/feeds/my", _reader));
        using var read = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{repost}", null, _reader);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using var list = await SendAsync(HttpMethod.Get, $"/api/v1/posts/{_original}/reposts", null, null);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
    }

    [Fact]
    public async Task OnlySomeoneElseWithAConfirmedEmailRepostsAPublishedPostOnce()
    {
        var unconfirmed = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(9));
        var draft = await SavePostAsync(_author, _authorBlog.Id, publish: false);

        using var guest = await RepostAsync(_original, null, account: null);
        using var withoutEmail = await RepostAsync(_original, null, unconfirmed);
        using var own = await RepostAsync(_original, null, _author);
        using var ofDraft = await RepostAsync(draft, null, _reposter);
        using var missing = await RepostAsync(Guid.NewGuid(), null, _reposter);
        using var tooLong = await RepostAsync(_original, new string('а', PostRules.RepostTextMaxLength + 1), _reposter);
        using var first = await RepostAsync(_original, null, _reposter);
        using var again = await RepostAsync(_original, "Ещё раз", _reposter);

        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutEmail.StatusCode);
        Assert.Equal(AccountPolicies.EmailNotConfirmedCode, await CodeOf(withoutEmail));
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Equal(RepostEndpoints.OwnPostCode, await CodeOf(own));
        Assert.Equal(HttpStatusCode.NotFound, ofDraft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(RepostEndpoints.AlreadyRepostedCode, await CodeOf(again));
    }

    [Fact]
    public async Task RepostOfARepostRepostsTheOriginalAndARepostIsNotEdited()
    {
        var other = await SaveConfirmedAccountAsync(4, "other@example.com");
        using var created = await RepostAsync(_original, null, _reposter);
        var repost = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var again = await RepostAsync(repost, null, other);
        using var edit = await SendAsync(
            HttpMethod.Put, $"/api/v1/posts/{repost}", """{"body":{"version":1,"blocks":[]}}""", _reposter);

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(_original, (await again.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("repostOfPostId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(PostEndpoints.RepostNotEditableCode, await CodeOf(edit));
    }

    [Fact]
    public async Task RepostIsAnnouncedToTheOriginalsAuthor()
    {
        var events = new ConcurrentQueue<PostReposted>();
        var endpoint = api.Services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            $"posts-tests-post-reposted-{Guid.NewGuid():N}",
            (_, e) => e.Handler<PostReposted>(c =>
            {
                events.Enqueue(c.Message);
                return Task.CompletedTask;
            }));
        await endpoint.Ready;
        try
        {
            using var created = await RepostAsync(_original, null, _reposter);
            var repost = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!events.Any(e => e.RepostId == repost))
            {
                Assert.True(DateTime.UtcNow < deadline, "PostReposted was not published in 30s");
                await Task.Delay(100, Ct);
            }

            var announced = Assert.Single(events, e => e.RepostId == repost);
            Assert.Equal((_original, _author, _reposter), (announced.PostId, announced.PostAuthorAccountId,
                announced.ReposterAccountId));
        }
        finally
        {
            await endpoint.StopAsync(CancellationToken.None);
        }
    }

    private Task<HttpResponseMessage> RepostAsync(Guid post, string? text, Guid? account) =>
        SendAsync(HttpMethod.Post, $"/api/v1/posts/{post}/reposts", JsonSerializer.Serialize(new { text }), account);

    private async Task<List<JsonElement>> ItemsAsync(string path, Guid? account)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray()];
    }

    private async Task<Guid> SavePostAsync(Guid author, Guid blog, bool publish = true)
    {
        var post = Post.CreateDraft(author, blog, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        if (publish)
        {
            post.Publish(api.Time.GetUtcNow());
        }

        await api.SaveAsync<PostsDbContext>(post);
        api.Time.Advance(TimeSpan.FromMinutes(1));
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
