using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Preferences.Hidden;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Preferences.IntegrationTests;

/// <summary>The published posts the Preferences module may see, without the Posts module.</summary>
public sealed class FakePostDirectory : IPostDirectory
{
    private readonly ConcurrentDictionary<Guid, PostHeadline> _published = new();

    public void Publish(Guid postId, string? title) => _published[postId] = new PostHeadline(postId, Guid.Empty, title, null);

    public void Remove(Guid postId) => _published.TryRemove(postId, out _);

    public Task<PostSummary?> FindPublishedAsync(Guid postId, CancellationToken cancellationToken) =>
        Task.FromResult(_published.ContainsKey(postId) ? new PostSummary(postId, Guid.Empty, Guid.Empty) : null);

    public Task<IReadOnlyDictionary<Guid, PostHeadline>> PublishedHeadlinesAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, PostHeadline>>(postIds
            .Where(_published.ContainsKey)
            .ToDictionary(id => id, id => _published[id]));

    public Task<IReadOnlyList<Guid>> PublishedPostIdsAsync(Guid blogId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);
}

/// <summary>The blogs and topics the Preferences module may see, without the Blogs module.</summary>
public sealed class FakeBlogDirectory : IBlogDirectory
{
    private readonly ConcurrentDictionary<Guid, BlogCard> _blogs = new();

    public void Add(Guid blogId, BlogKind kind, string? name, string? nickname) =>
        _blogs[blogId] = new BlogCard(blogId, kind, null, name, nickname, []);

    public Task<BlogSummary?> FindAsync(Guid blogId, CancellationToken cancellationToken) =>
        Task.FromResult(_blogs.TryGetValue(blogId, out var card)
            ? new BlogSummary(card.Id, card.Kind, card.OwnerAccountId)
            : null);

    public Task<BlogSummary> EnsurePersonalBlogAsync(Guid accountId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<BlogCard>> CardsAsync(
        IReadOnlyCollection<Guid> blogIds,
        IReadOnlyCollection<Guid> ownerAccountIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BlogCard>>([.. blogIds.Where(_blogs.ContainsKey).Select(id => _blogs[id])]);
}

/// <summary>The API host with the posts and blogs of <see cref="FakePostDirectory"/> and <see cref="FakeBlogDirectory"/>.</summary>
public sealed class HiddenApi : IntegrationApiFactory
{
    public HiddenApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) =>
        WithModule(services =>
        {
            services.AddTestAuthentication();
            services.RemoveAll<IPostDirectory>();
            services.AddSingleton<IPostDirectory>(Posts);
            services.RemoveAll<IBlogDirectory>();
            services.AddSingleton<IBlogDirectory>(Blogs);
        });

    public FakePostDirectory Posts { get; } = new();

    public FakeBlogDirectory Blogs { get; } = new();
}

/// <summary>
/// E11-07 (ADR-0119): the posts and topics the signed-in account hid from its feeds through
/// <c>/api/v1/preferences/me/hidden</c> — hide, read the lists latest first with their names, show again, every change
/// repeated with the same result; an unknown post or topic and a personal blog refused; no access for a guest; one
/// account's lists are not another's; what the feeds get from them.
/// </summary>
public sealed class HiddenApiTests(HiddenApi api) : IClassFixture<HiddenApi>, IAsyncLifetime
{
    private const string _hidden = "/api/v1/preferences/me/hidden";
    private readonly Guid _anna = Guid.CreateVersion7();
    private readonly Guid _boris = Guid.CreateVersion7();
    private readonly Guid _post = Guid.CreateVersion7();
    private readonly Guid _otherPost = Guid.CreateVersion7();
    private readonly Guid _topic = Guid.CreateVersion7();
    private readonly Guid _otherTopic = Guid.CreateVersion7();
    private readonly Guid _blog = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        api.Posts.Publish(_post, "Первый пост");
        api.Posts.Publish(_otherPost, null);
        api.Blogs.Add(_topic, BlogKind.Topic, "Книги", "books");
        api.Blogs.Add(_otherTopic, BlogKind.Topic, "Игры", "games");
        api.Blogs.Add(_blog, BlogKind.Personal, "Анна", "anna");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task HiddenPostsAreListedLatestFirstWithTitlesAndShowingOneBringsItBack()
    {
        using var client = api.CreateClient();

        using var first = await SendAsync(client, HttpMethod.Put, $"{_hidden}/posts/{_post}", _anna);
        api.Time.Advance(TimeSpan.FromMinutes(1));
        using var second = await SendAsync(client, HttpMethod.Put, $"{_hidden}/posts/{_otherPost}", _anna);
        using var again = await SendAsync(client, HttpMethod.Put, $"{_hidden}/posts/{_post}", _anna);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        var list = await ListAsync(client, _anna);
        var posts = list.GetProperty("posts").EnumerateArray().ToList();
        Assert.Equal([_otherPost, _post], posts.Select(p => p.GetProperty("postId").GetGuid()));
        Assert.Equal("Первый пост", posts[1].GetProperty("title").GetString());
        Assert.False(posts[1].GetProperty("gone").GetBoolean());
        Assert.Equal(HiddenRules.MaxHidden, list.GetProperty("max").GetInt32());

        using var shown = await SendAsync(client, HttpMethod.Delete, $"{_hidden}/posts/{_post}", _anna);
        using var shownAgain = await SendAsync(client, HttpMethod.Delete, $"{_hidden}/posts/{_post}", _anna);

        Assert.Equal(HttpStatusCode.NoContent, shown.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, shownAgain.StatusCode);
        Assert.Equal(
            [_otherPost],
            (await ListAsync(client, _anna)).GetProperty("posts").EnumerateArray().Select(p => p.GetProperty("postId").GetGuid()));
    }

    [Fact]
    public async Task HiddenTopicsAreListedWithTheirNamesAndTheirStateIsKnown()
    {
        using var client = api.CreateClient();

        using var before = await SendAsync(client, HttpMethod.Get, $"{_hidden}/topics/{_topic}", _anna);
        using var hidden = await SendAsync(client, HttpMethod.Put, $"{_hidden}/topics/{_topic}", _anna);
        using var after = await SendAsync(client, HttpMethod.Get, $"{_hidden}/topics/{_topic}", _anna);

        Assert.Equal(HttpStatusCode.NoContent, hidden.StatusCode);
        Assert.False((await before.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("hidden").GetBoolean());
        Assert.True((await after.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("hidden").GetBoolean());
        var topic = Assert.Single((await ListAsync(client, _anna)).GetProperty("topics").EnumerateArray().ToList());
        Assert.Equal(_topic, topic.GetProperty("blogId").GetGuid());
        Assert.Equal("Книги", topic.GetProperty("name").GetString());
        Assert.Equal("books", topic.GetProperty("nickname").GetString());

        using var shown = await SendAsync(client, HttpMethod.Delete, $"{_hidden}/topics/{_topic}", _anna);

        Assert.Equal(HttpStatusCode.NoContent, shown.StatusCode);
        Assert.Empty((await ListAsync(client, _anna)).GetProperty("topics").EnumerateArray());
    }

    [Fact]
    public async Task APostThatIsGoneStaysInTheListToBeShownAgain()
    {
        using var client = api.CreateClient();
        using var hidden = await SendAsync(client, HttpMethod.Put, $"{_hidden}/posts/{_post}", _anna);
        api.Posts.Remove(_post);

        var post = Assert.Single((await ListAsync(client, _anna)).GetProperty("posts").EnumerateArray().ToList());

        Assert.True(post.GetProperty("gone").GetBoolean());
        Assert.Equal(JsonValueKind.Null, post.GetProperty("title").ValueKind);
    }

    [Fact]
    public async Task AnUnknownPostOrTopicIsNotFoundAndAPersonalBlogIsAValidationError()
    {
        using var client = api.CreateClient();

        using var post = await SendAsync(client, HttpMethod.Put, $"{_hidden}/posts/{Guid.CreateVersion7()}", _anna);
        using var topic = await SendAsync(client, HttpMethod.Put, $"{_hidden}/topics/{Guid.CreateVersion7()}", _anna);
        using var blog = await SendAsync(client, HttpMethod.Put, $"{_hidden}/topics/{_blog}", _anna);

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HiddenEndpoints.PostNotFoundCode, await CodeOf(post));
        Assert.Equal(HttpStatusCode.NotFound, topic.StatusCode);
        Assert.Equal(HiddenEndpoints.TopicNotFoundCode, await CodeOf(topic));
        Assert.Equal(HttpStatusCode.BadRequest, blog.StatusCode);
        Assert.Equal("validation_failed", await CodeOf(blog));
        var list = await ListAsync(client, _anna);
        Assert.Empty(list.GetProperty("posts").EnumerateArray());
        Assert.Empty(list.GetProperty("topics").EnumerateArray());
    }

    [Fact]
    public async Task OneAccountsListsAreNotAnothers()
    {
        using var client = api.CreateClient();

        using var hidden = await SendAsync(client, HttpMethod.Put, $"{_hidden}/topics/{_topic}", _anna);
        // Boris showing the topic in his own feeds leaves Anna's list as it is.
        using var shown = await SendAsync(client, HttpMethod.Delete, $"{_hidden}/topics/{_topic}", _boris);

        Assert.Empty((await ListAsync(client, _boris)).GetProperty("topics").EnumerateArray());
        Assert.Single((await ListAsync(client, _anna)).GetProperty("topics").EnumerateArray());
    }

    [Fact]
    public async Task GuestHasNoHiddenLists()
    {
        using var client = api.CreateClient();

        using var read = await SendAsync(client, HttpMethod.Get, _hidden, null);
        using var state = await SendAsync(client, HttpMethod.Get, $"{_hidden}/topics/{_topic}", null);
        using var hidePost = await SendAsync(client, HttpMethod.Put, $"{_hidden}/posts/{_post}", null);
        using var hideTopic = await SendAsync(client, HttpMethod.Put, $"{_hidden}/topics/{_topic}", null);
        using var show = await SendAsync(client, HttpMethod.Delete, $"{_hidden}/posts/{_post}", null);

        Assert.All(
            [read, state, hidePost, hideTopic, show],
            r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    [Fact]
    public async Task TheFeedsLeaveOutTheHiddenPostsAndTopics()
    {
        using var client = api.CreateClient();
        using var post = await SendAsync(client, HttpMethod.Put, $"{_hidden}/posts/{_post}", _anna);
        using var topic = await SendAsync(client, HttpMethod.Put, $"{_hidden}/topics/{_topic}", _anna);

        await using var scope = api.Services.CreateAsyncScope();
        var feeds = scope.ServiceProvider.GetServices<IFeedExclusionSource>().OfType<HiddenFromFeeds>().Single();

        var excluded = await feeds.ForReaderAsync(_anna, Ct);
        Assert.Equal([_post], excluded.Posts);
        Assert.Equal([_topic], excluded.Blogs);
        Assert.Empty(excluded.Authors);
        Assert.Same(FeedExclusions.None, await feeds.ForReaderAsync(_boris, Ct));
    }

    private static async Task<JsonElement> ListAsync(HttpClient client, Guid account)
    {
        using var response = await SendAsync(client, HttpMethod.Get, _hidden, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, Guid? account)
    {
        using var request = new HttpRequestMessage(method, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await client.SendAsync(request, Ct);
    }
}
