using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs.Contracts;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E11-04 (FR-NOTIFY, ADR-0118): the «колокольчик» of a subscription — turning the notifications about new posts on and
/// off, the setting kept on the subscription and shown in the profile, and the contract that names who is notified.
/// </summary>
public sealed class NewPostNotificationApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
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
    public async Task ASubscriberTurnsTheBellOnAndOffAndTheProfileShowsIt()
    {
        var topic = await TopicAsync();
        var reader = NewAccount();
        var subscribed = await SendAsync(HttpMethod.Put, topic.Id, reader, "");
        Assert.False(subscribed.Body.GetProperty("notifyNewPosts").GetBoolean());

        var on = await SendAsync(HttpMethod.Put, topic.Id, reader);
        var again = await SendAsync(HttpMethod.Put, topic.Id, reader);

        Assert.Equal(HttpStatusCode.OK, on.Status);
        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.True(again.Body.GetProperty("notifyNewPosts").GetBoolean());
        Assert.True(again.Body.GetProperty("subscribed").GetBoolean());
        Assert.Equal(1, again.Body.GetProperty("subscriberCount").GetInt32());
        Assert.True((await ProfileAsync(topic.Id, reader)).GetProperty("notifyNewPosts").GetBoolean());
        Assert.False((await ProfileAsync(topic.Id)).GetProperty("notifyNewPosts").GetBoolean());

        var off = await SendAsync(HttpMethod.Delete, topic.Id, reader);

        Assert.Equal(HttpStatusCode.OK, off.Status);
        Assert.False(off.Body.GetProperty("notifyNewPosts").GetBoolean());
        Assert.True(off.Body.GetProperty("subscribed").GetBoolean());
        Assert.False((await ProfileAsync(topic.Id, reader)).GetProperty("notifyNewPosts").GetBoolean());
    }

    // The setting is on the subscription: unsubscribing drops it, a new subscription starts without it.
    [Fact]
    public async Task UnsubscribingDropsTheBell()
    {
        var topic = await TopicAsync();
        var reader = NewAccount();
        await SendAsync(HttpMethod.Put, topic.Id, reader, "");
        await SendAsync(HttpMethod.Put, topic.Id, reader);

        await SendAsync(HttpMethod.Delete, topic.Id, reader, "");
        var back = await SendAsync(HttpMethod.Put, topic.Id, reader, "");

        Assert.False(back.Body.GetProperty("notifyNewPosts").GetBoolean());
    }

    [Fact]
    public async Task OnlyASubscriberHasABell()
    {
        var topic = await TopicAsync();
        var reader = NewAccount();

        var on = await SendAsync(HttpMethod.Put, topic.Id, reader);
        var off = await SendAsync(HttpMethod.Delete, topic.Id, reader);

        Assert.Equal(HttpStatusCode.Conflict, on.Status);
        Assert.Equal("blogs.not_subscribed", on.Body.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, off.Status);
    }

    [Fact]
    public async Task AGuestIsAskedToSignInAndAMissingBlogIsNotFound()
    {
        var topic = await TopicAsync();
        using var guest = await _client.PutAsync(
            new Uri($"/api/v1/blogs/{topic.Id}/subscription/notifications", UriKind.Relative), null, Ct);
        var missing = await SendAsync(HttpMethod.Put, Guid.CreateVersion7(), NewAccount());

        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.Status);
        Assert.Equal("blogs.blog_not_found", missing.Body.GetProperty("code").GetString());
    }

    // The contract for the Notifications module: the subscribers with the bell on of the post's blog and of its author's
    // personal blog, each once, never the author.
    [Fact]
    public async Task OtherModulesReadWhoIsNotifiedAboutANewPost()
    {
        var author = NewAccount();
        var personal = Subsite.CreatePersonal(author, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(personal);
        var topic = await TopicAsync();
        var both = NewAccount();
        var topicOnly = NewAccount();
        var blogOnly = NewAccount();
        var silent = NewAccount();
        await BellAsync(both, topic.Id, personal.Id);
        await BellAsync(topicOnly, topic.Id);
        await BellAsync(blogOnly, personal.Id);
        await BellAsync(author, topic.Id);
        await SendAsync(HttpMethod.Put, topic.Id, silent, "");

        await using var scope = api.Services.CreateAsyncScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<IBlogSubscriptions>();
        var inTopic = await subscriptions.NewPostRecipientsAsync(topic.Id, author, Ct);
        var inBlog = await subscriptions.NewPostRecipientsAsync(personal.Id, author, Ct);
        var byOther = await subscriptions.NewPostRecipientsAsync(topic.Id, NewAccount(), Ct);

        Assert.Equal(new[] { both, topicOnly, blogOnly }.Order(), inTopic.Order());
        Assert.Equal(new[] { both, blogOnly }.Order(), inBlog.Order());
        Assert.Equal(new[] { both, topicOnly, author }.Order(), byOther.Order());
    }

    private Guid NewAccount() => Guid.CreateVersion7(api.Time.GetUtcNow());

    private async Task BellAsync(Guid reader, params Guid[] blogs)
    {
        foreach (var blog in blogs)
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, blog, reader, "")).Status);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, blog, reader)).Status);
        }
    }

    private async Task<Subsite> TopicAsync()
    {
        var owner = NewAccount();
        var topic = Subsite.CreateTopic("Наука", $"bell{owner.ToString("N")[^12..]}", owner, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        return topic;
    }

    // `…/subscription` with the suffix: "/notifications" by default, "" for the subscription itself.
    private async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(
        HttpMethod method, Guid blogId, Guid account, string suffix = "/notifications")
    {
        using var request = new HttpRequestMessage(method, $"/api/v1/blogs/{blogId}/subscription{suffix}");
        request.As(account.ToString("D"));
        using var response = await _client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private async Task<JsonElement> ProfileAsync(Guid blogId, Guid? account = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/profiles/{blogId}");
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        using var response = await _client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
