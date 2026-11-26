using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E11-03 (FR-SUB, ADR-0085): subscribing to a blog or a topic and unsubscribing, both idempotent; the numbers and the
/// reader's own mark in the profile; the contract other modules read.
/// </summary>
public sealed class SubscriptionApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
{
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SubscribingTwiceIsOneSubscriptionAndTheProfileShowsIt()
    {
        var (topic, _) = await TopicAsync();
        var reader = NewAccount();

        var first = await SendAsync(HttpMethod.Put, topic.Id, reader);
        var again = await SendAsync(HttpMethod.Put, topic.Id, reader);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Equal((1, true), Read(again.Body));
        var own = await ProfileAsync(topic.Id, reader);
        Assert.Equal(1, own.GetProperty("subscriberCount").GetInt32());
        Assert.True(own.GetProperty("subscribed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, own.GetProperty("subscriptionCount").ValueKind);
        var guest = await ProfileAsync(topic.Id);
        Assert.Equal(1, guest.GetProperty("subscriberCount").GetInt32());
        Assert.False(guest.GetProperty("subscribed").GetBoolean());
    }

    [Fact]
    public async Task OnlyANewSubscriptionIsPublishedWithTheBlogsOwner()
    {
        var events = new System.Collections.Concurrent.ConcurrentQueue<BlogSubscribed>();
        var endpoint = api.Services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            $"blogs-tests-blog-subscribed-{Guid.NewGuid():N}",
            (_, e) => e.Handler<BlogSubscribed>(c =>
            {
                events.Enqueue(c.Message);
                return Task.CompletedTask;
            }));
        await endpoint.Ready;
        try
        {
            var (topic, owner) = await TopicAsync();
            var reader = NewAccount();
            var marker = NewAccount();

            await SendAsync(HttpMethod.Put, topic.Id, reader);
            await SendAsync(HttpMethod.Put, topic.Id, reader);
            await SendAsync(HttpMethod.Delete, topic.Id, reader);
            await SendAsync(HttpMethod.Put, topic.Id, marker);

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!events.Any(e => e.SubscriberAccountId == marker))
            {
                Assert.True(DateTime.UtcNow < deadline, "the subscriptions were not published in 30s");
                await Task.Delay(100, Ct);
            }

            var mine = Assert.Single(events, e => e.SubscriberAccountId == reader);
            Assert.Equal((topic.Id, BlogKind.Topic, (Guid?)owner), (mine.BlogId, mine.Kind, mine.OwnerAccountId));
        }
        finally
        {
            await endpoint.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UnsubscribingTwiceLeavesNoSubscriptionAndOthersStay()
    {
        var (topic, _) = await TopicAsync();
        var reader = NewAccount();
        var other = NewAccount();
        await SendAsync(HttpMethod.Put, topic.Id, reader);
        await SendAsync(HttpMethod.Put, topic.Id, other);

        var first = await SendAsync(HttpMethod.Delete, topic.Id, reader);
        var again = await SendAsync(HttpMethod.Delete, topic.Id, reader);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Equal((1, false), Read(again.Body));
        Assert.Equal(1, (await ProfileAsync(topic.Id)).GetProperty("subscriberCount").GetInt32());
    }

    [Fact]
    public async Task RequestsAtTheSameMomentMakeOneSubscription()
    {
        var (topic, _) = await TopicAsync();
        var reader = NewAccount();

        var answers = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SendAsync(HttpMethod.Put, topic.Id, reader)));

        Assert.All(answers, a => Assert.Equal(HttpStatusCode.OK, a.Status));
        Assert.Equal(1, (await ProfileAsync(topic.Id)).GetProperty("subscriberCount").GetInt32());
    }

    [Fact]
    public async Task APersonalBlogCountsItsSubscribersAndTheOwnersSubscriptions()
    {
        var now = api.Time.GetUtcNow();
        var anna = NewAccount();
        var annasBlog = Subsite.CreatePersonal(anna, now);
        var boris = NewAccount();
        var borisBlog = Subsite.CreatePersonal(boris, now);
        var (topic, _) = await TopicAsync();
        await api.SaveAsync<BlogsDbContext>(annasBlog, borisBlog);

        await SendAsync(HttpMethod.Put, borisBlog.Id, anna);
        await SendAsync(HttpMethod.Put, topic.Id, anna);
        await SendAsync(HttpMethod.Put, annasBlog.Id, boris);

        var annas = await ProfileAsync(annasBlog.Id, anna);
        Assert.Equal(1, annas.GetProperty("subscriberCount").GetInt32());
        Assert.Equal(2, annas.GetProperty("subscriptionCount").GetInt32());
        Assert.False(annas.GetProperty("subscribed").GetBoolean());
        var own = await GetAsync("/api/v1/profiles/me", anna);
        Assert.Equal(2, own.GetProperty("subscriptionCount").GetInt32());
        Assert.Equal(1, own.GetProperty("subscriberCount").GetInt32());
        Assert.True((await ProfileAsync(borisBlog.Id, anna)).GetProperty("subscribed").GetBoolean());
    }

    [Fact]
    public async Task NoOneSubscribesToTheirOwnBlogButATopicOwnerFollowsTheTopic()
    {
        var now = api.Time.GetUtcNow();
        var anna = NewAccount();
        var blog = Subsite.CreatePersonal(anna, now);
        await api.SaveAsync<BlogsDbContext>(blog);
        var (topic, owner) = await TopicAsync();

        var ownBlog = await SendAsync(HttpMethod.Put, blog.Id, anna);
        var ownTopic = await SendAsync(HttpMethod.Put, topic.Id, owner);

        Assert.Equal(HttpStatusCode.Forbidden, ownBlog.Status);
        Assert.Equal(SubscriptionEndpoints.OwnBlogCode, ownBlog.Body.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, ownTopic.Status);
        Assert.Equal(0, (await ProfileAsync(blog.Id)).GetProperty("subscriberCount").GetInt32());
    }

    [Fact]
    public async Task AGuestIsAskedToSignIn()
    {
        var (topic, _) = await TopicAsync();

        using var put = await _client.PutAsync(new Uri($"/api/v1/blogs/{topic.Id}/subscription", UriKind.Relative), null, Ct);
        using var delete = await _client.DeleteAsync(new Uri($"/api/v1/blogs/{topic.Id}/subscription", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public async Task AMissingBlogIsNotFoundAndABadIdIsNoRoute()
    {
        var reader = NewAccount();

        var put = await SendAsync(HttpMethod.Put, Guid.CreateVersion7(), reader);
        var delete = await SendAsync(HttpMethod.Delete, Guid.CreateVersion7(), reader);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/blogs/not-a-blog/subscription");
        request.As(reader.ToString("D"));
        using var bad = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotFound, put.Status);
        Assert.Equal(SubscriptionEndpoints.BlogNotFoundCode, put.Body.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, delete.Status);
        Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);
    }

    [Fact]
    public async Task OtherModulesReadTheSubscriptionsNewestFirst()
    {
        var now = api.Time.GetUtcNow();
        var reader = NewAccount();
        var author = NewAccount();
        var blog = Subsite.CreatePersonal(author, now);
        await api.SaveAsync<BlogsDbContext>(blog);
        var (topic, _) = await TopicAsync();
        await SendAsync(HttpMethod.Put, blog.Id, reader);
        api.Time.Advance(TimeSpan.FromMinutes(1));
        await SendAsync(HttpMethod.Put, topic.Id, reader);

        await using var scope = api.Services.CreateAsyncScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<IBlogSubscriptions>();
        var blogs = await subscriptions.SubscribedBlogsAsync(reader, Ct);

        Assert.Equal(
            [new BlogSummary(topic.Id, BlogKind.Topic, topic.OwnerAccountId), new BlogSummary(blog.Id, BlogKind.Personal, author)],
            blogs);
        Assert.True(await subscriptions.IsSubscribedAsync(reader, topic.Id, Ct));
        Assert.False(await subscriptions.IsSubscribedAsync(author, topic.Id, Ct));
        Assert.Empty(await subscriptions.SubscribedBlogsAsync(author, Ct));
    }

    private Guid NewAccount() => Guid.CreateVersion7(api.Time.GetUtcNow());

    private async Task<(Subsite Topic, Guid Owner)> TopicAsync()
    {
        var owner = NewAccount();
        var topic = Subsite.CreateTopic("Наука", $"sub{owner.ToString("N")[^12..]}", owner, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        return (topic, owner);
    }

    private static (int Count, bool Subscribed) Read(JsonElement body) =>
        (body.GetProperty("subscriberCount").GetInt32(), body.GetProperty("subscribed").GetBoolean());

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(HttpMethod method, Guid blogId, Guid account)
    {
        using var request = new HttpRequestMessage(method, $"/api/v1/blogs/{blogId}/subscription");
        request.As(account.ToString("D"));
        using var response = await _client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private Task<JsonElement> ProfileAsync(Guid blogId, Guid? account = null) => GetAsync($"/api/v1/profiles/{blogId}", account);

    private async Task<JsonElement> GetAsync(string path, Guid? account = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        using var response = await _client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}

/// <summary>The rules of a subscription apart from the database (ADR-0085).</summary>
public sealed class SubscriptionRulesTests
{
    private static readonly Guid _account = Guid.CreateVersion7();
    private static readonly Guid _other = Guid.CreateVersion7();

    [Fact]
    public void OwnPersonalBlogIsRefused() =>
        Assert.Equal(SubscriptionRefusal.OwnBlog, SubscriptionRules.Refusal(_account, SubsiteType.Personal, _account));

    [Fact]
    public void SomeoneElsesBlogAndOwnTopicAreAllowed()
    {
        Assert.Null(SubscriptionRules.Refusal(_account, SubsiteType.Personal, _other));
        Assert.Null(SubscriptionRules.Refusal(_account, SubsiteType.Topic, _account));
        Assert.Null(SubscriptionRules.Refusal(_account, SubsiteType.Topic, null));
    }

    [Fact]
    public void ASubscriptionNeedsTheAccountAndTheBlog()
    {
        Assert.Throws<ArgumentException>(() => Subscription.Create(Guid.Empty, _other, DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => Subscription.Create(_account, Guid.Empty, DateTimeOffset.UnixEpoch));
    }
}
