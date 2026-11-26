using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Modules.Posts.Contracts;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// A stand-in for the black list (E11-06) and the hidden posts and topics (E11-07): what each reader keeps out, set by
/// the test.
/// </summary>
public sealed class FakeFeedExclusions : IFeedExclusionSource
{
    private readonly ConcurrentDictionary<Guid, FeedExclusions> _byReader = new();

    public void Set(Guid reader, FeedExclusions exclusions) => _byReader[reader] = exclusions;

    public Task<FeedExclusions> ForReaderAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(_byReader.GetValueOrDefault(accountId, FeedExclusions.None));
}

/// <summary>The Api host signed in by the test scheme, with <see cref="FakeFeedExclusions"/> as the one source of exclusions.</summary>
public sealed class MyFeedApi : IntegrationApiFactory
{
    public MyFeedApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(services =>
        {
            services.AddTestAuthentication();
            services.AddSingleton<FakeFeedExclusions>();
            services.AddSingleton<IFeedExclusionSource>(sp => sp.GetRequiredService<FakeFeedExclusions>());
        });

    public FakeFeedExclusions Exclusions => Services.GetRequiredService<FakeFeedExclusions>();
}

/// <summary>
/// E08-04: «Моя лента» <c>/api/v1/feeds/my</c> (ADR-0089) gives the signed-in reader the published posts of the blogs
/// and topics they are subscribed to and every post of the people they follow, newest first, page by page with the
/// cursor of the fresh feed; hidden posts and topics and blocked authors are left out; one reader's feed is never
/// another's.
/// </summary>
public sealed class MyFeedApiTests(MyFeedApi api) : IClassFixture<MyFeedApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _reader;
    private Guid _author;
    private Guid _stranger;
    private Subsite _authorBlog = null!;
    private Subsite _topic = null!;
    private Subsite _otherTopic = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        var now = api.Time.GetUtcNow();
        _reader = Guid.CreateVersion7(now);
        _author = Guid.CreateVersion7(now.AddSeconds(1));
        _stranger = Guid.CreateVersion7(now.AddSeconds(2));
        var tag = _reader.ToString("N")[..8];
        _authorBlog = Subsite.CreatePersonal(_author, now);
        _topic = Subsite.CreateTopic("Наука", $"science{tag}", _stranger, now);
        _otherTopic = Subsite.CreateTopic("Кино", $"cinema{tag}", _stranger, now);
        await api.SaveAsync<BlogsDbContext>(_authorBlog, _topic, _otherTopic);
        await api.SaveAsync<BlogsDbContext>(
            Subscription.Create(_reader, _authorBlog.Id, now),
            Subscription.Create(_reader, _topic.Id, now));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SubscribersFeedHasTheSubscribedBlogsAndTheFollowedAuthorsPostsNewestFirstPageByPage()
    {
        var inAuthorBlog = await SaveAsync(_author, _authorBlog.Id);
        var inTopic = await SaveAsync(_stranger, _topic.Id);
        var authorElsewhere = await SaveAsync(_author, _otherTopic.Id);
        await SaveAsync(_stranger, _otherTopic.Id);

        var (first, cursor) = await PageAsync(_reader, "?limit=2");
        var (last, end) = await PageAsync(_reader, $"?limit=2&cursor={Uri.EscapeDataString(cursor!)}");

        Assert.Equal([authorElsewhere, inTopic, inAuthorBlog], [.. first, .. last]);
        Assert.Null(end);
    }

    [Fact]
    public async Task DraftsAndDeletedPostsAreNotInIt()
    {
        var shown = await SaveAsync(_author, _authorBlog.Id);
        await SaveAsync(_author, _authorBlog.Id, publish: false);
        await SaveAsync(_stranger, _topic.Id, delete: true);

        Assert.Equal([shown], (await PageAsync(_reader)).Items);
    }

    [Fact]
    public async Task HiddenPostsHiddenTopicsAndBlockedAuthorsAreLeftOut()
    {
        var shown = await SaveAsync(_author, _authorBlog.Id);
        var hidden = await SaveAsync(_author, _authorBlog.Id);
        await SaveAsync(_author, _topic.Id);
        var blocked = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(3));
        await SaveAsync(blocked, _topic.Id);
        var other = Subsite.CreateTopic("Игры", $"games{_reader.ToString("N")[..8]}", _stranger, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(other);
        await api.SaveAsync<BlogsDbContext>(Subscription.Create(_reader, other.Id, api.Time.GetUtcNow()));
        var inOther = await SaveAsync(_stranger, other.Id);
        api.Exclusions.Set(_reader, new FeedExclusions([hidden], [_topic.Id], [blocked]));

        Assert.Equal([inOther, shown], (await PageAsync(_reader)).Items);
    }

    [Fact]
    public async Task ReaderWithNoSubscriptionsHasAnEmptyLastPage()
    {
        await SaveAsync(_author, _authorBlog.Id);

        var (items, cursor) = await PageAsync(_stranger);

        Assert.Empty(items);
        Assert.Null(cursor);
    }

    [Fact]
    public async Task OneReadersFeedIsNeverAnothersAndIsNotToBeCached()
    {
        await SaveAsync(_author, _authorBlog.Id);
        Assert.Single((await PageAsync(_reader)).Items);

        using var response = await SendAsync("/api/v1/feeds/my", _stranger);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
    }

    [Fact]
    public async Task GuestIsUnauthorized()
    {
        using var response = await SendAsync("/api/v1/feeds/my", account: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("?limit=0", "validation_failed")]
    [InlineData("?cursor=not-a-cursor", "invalid_cursor")]
    public async Task WrongPageIsABadRequest(string query, string code)
    {
        using var response = await SendAsync($"/api/v1/feeds/my{query}", _reader);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public void ExclusionsOfSeveralSourcesAddUp()
    {
        var (post, blog, author) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        var all = new FeedExclusions([post], [], [author]).Union(new FeedExclusions([post], [blog], []));

        Assert.Equal([post], all.Posts);
        Assert.Equal([blog], all.Blogs);
        Assert.Equal([author], all.Authors);
    }

    // Every post is published a minute after the one before, so the order of saving is the order of the feed.
    private async Task<Guid> SaveAsync(Guid author, Guid blog, bool publish = true, bool delete = false)
    {
        api.Time.Advance(TimeSpan.FromMinutes(1));
        var post = Post.CreateDraft(author, blog, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        if (publish)
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

    private async Task<(List<Guid> Items, string? Cursor)> PageAsync(Guid reader, string query = "")
    {
        using var response = await SendAsync($"/api/v1/feeds/my{query}", reader);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var next = page.GetProperty("nextCursor");
        return ([.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())],
            next.ValueKind == JsonValueKind.Null ? null : next.GetString());
    }

    private async Task<HttpResponseMessage> SendAsync(string path, Guid? account)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await _client.SendAsync(request, Ct);
    }
}
