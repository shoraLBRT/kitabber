using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E04-06 (FR-PROFILE, ADR-0099): the subscriber list of a blog or a topic and the subscription list of a personal blog —
/// the newest first, page by page, exactly the rows the header counts, with the reader's own marks for «Подписаться».
/// </summary>
public sealed class SubscriptionListApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
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
    public async Task TheSubscribersAreTheNewestFirstAndAsManyAsTheHeaderCounts()
    {
        var blog = await PersonalAsync("Автор");
        var first = await PersonalAsync("Первый");
        var second = await PersonalAsync("Второй");
        await FollowAsync(first, blog);
        await FollowAsync(second, blog);

        var page = await GetAsync($"/api/v1/blogs/{blog.Id}/subscribers");
        var profile = await GetAsync($"/api/v1/profiles/{blog.Id}");

        Assert.Equal([second.Id, first.Id], Ids(page));
        Assert.Equal(["Второй", "Первый"], Items(page).Select(c => c.GetProperty("name").GetString()));
        Assert.All(Items(page), c => Assert.Equal("personal", c.GetProperty("type").GetString()));
        Assert.Equal(profile.GetProperty("subscriberCount").GetInt32(), Items(page).Length);
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task TheSubscriptionsOfAPersonalBlogListItsBlogsAndTopicsAsManyAsTheHeaderCounts()
    {
        var reader = await PersonalAsync("Читатель");
        var author = await PersonalAsync("Автор");
        var topic = Subsite.CreateTopic("Наука", $"sci{Guid.NewGuid():N}"[..20], Guid.CreateVersion7(), api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        await FollowAsync(reader, author);
        await FollowAsync(reader, topic);

        var page = await GetAsync($"/api/v1/blogs/{reader.Id}/subscriptions");
        var profile = await GetAsync($"/api/v1/profiles/{reader.Id}");

        Assert.Equal([topic.Id, author.Id], Ids(page));
        Assert.Equal(["topic", "personal"], Items(page).Select(c => c.GetProperty("type").GetString()));
        Assert.Equal(profile.GetProperty("subscriptionCount").GetInt32(), Items(page).Length);
    }

    [Fact]
    public async Task TheListsGoPageByPageWithoutLosingRowsOfTheSameMoment()
    {
        var blog = await PersonalAsync("Популярный");
        var now = api.Time.GetUtcNow();
        for (var i = 0; i < 5; i++)
        {
            var follower = await PersonalAsync($"Читатель {i}", advance: false);
            await api.SaveAsync<BlogsDbContext>(Subscription.Create(follower.OwnerAccountId!.Value, blog.Id, now));
        }

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await GetAsync(
                $"/api/v1/blogs/{blog.Id}/subscribers?limit=2" + (cursor is null ? "" : $"&cursor={cursor}"));
            seen.AddRange(Ids(page));
            cursor = page.GetProperty("nextCursor").GetString();
            pages++;
        }
        while (cursor is not null);

        Assert.Equal(3, pages);
        Assert.Equal(5, seen.Distinct().Count());
    }

    [Fact]
    public async Task AReaderSeesWhomTheyFollowAndTheirOwnBlogWithoutAButton()
    {
        var topic = Subsite.CreateTopic("Игры", $"gm{Guid.NewGuid():N}"[..20], Guid.CreateVersion7(), api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        var reader = await PersonalAsync("Я");
        var friend = await PersonalAsync("Друг");
        var stranger = await PersonalAsync("Незнакомец");
        await FollowAsync(stranger, topic);
        await FollowAsync(friend, topic);
        await FollowAsync(reader, topic);
        await FollowAsync(reader, friend);

        var signedIn = Items(await GetAsync($"/api/v1/blogs/{topic.Id}/subscribers", reader.OwnerAccountId));
        var guest = Items(await GetAsync($"/api/v1/blogs/{topic.Id}/subscribers"));

        Assert.Equal([reader.Id, friend.Id, stranger.Id], signedIn.Select(c => c.GetProperty("id").GetGuid()));
        Assert.Equal([false, true, false], signedIn.Select(c => c.GetProperty("subscribed").GetBoolean()));
        Assert.Equal([true, false, false], signedIn.Select(c => c.GetProperty("own").GetBoolean()));
        Assert.All(guest, c =>
        {
            Assert.False(c.GetProperty("subscribed").GetBoolean());
            Assert.False(c.GetProperty("own").GetBoolean());
        });
    }

    [Fact]
    public async Task AnEmptyListIsAnEmptyPage()
    {
        var blog = await PersonalAsync("Новичок");

        Assert.Empty(Items(await GetAsync($"/api/v1/blogs/{blog.Id}/subscribers")));
        Assert.Empty(Items(await GetAsync($"/api/v1/blogs/{blog.Id}/subscriptions")));
    }

    [Fact]
    public async Task AMissingBlogATopicsSubscriptionsAndABadPageAreRefused()
    {
        var topic = Subsite.CreateTopic("Кино", $"cn{Guid.NewGuid():N}"[..20], Guid.CreateVersion7(), api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);

        var missing = await _client.GetAsync($"/api/v1/blogs/{Guid.NewGuid()}/subscribers", Ct);
        var missingSubscriptions = await _client.GetAsync($"/api/v1/blogs/{Guid.NewGuid()}/subscriptions", Ct);
        var ofTopic = await _client.GetAsync($"/api/v1/blogs/{topic.Id}/subscriptions", Ct);
        var cursor = await _client.GetAsync($"/api/v1/blogs/{topic.Id}/subscribers?cursor=broken", Ct);
        var limit = await _client.GetAsync($"/api/v1/blogs/{topic.Id}/subscribers?limit=0", Ct);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(SubscriptionEndpoints.BlogNotFoundCode, await CodeAsync(missing));
        Assert.Equal(HttpStatusCode.NotFound, missingSubscriptions.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ofTopic.StatusCode);
        Assert.Equal(SubscriptionListEndpoints.NoSubscriptionsCode, await CodeAsync(ofTopic));
        Assert.Equal(HttpStatusCode.BadRequest, cursor.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, limit.StatusCode);
    }

    private async Task<Subsite> PersonalAsync(string name, bool advance = true)
    {
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(api.Time.GetUtcNow()), api.Time.GetUtcNow());
        blog.Rename(name);
        await api.SaveAsync<BlogsDbContext>(blog);
        if (advance)
        {
            api.Time.Advance(TimeSpan.FromSeconds(1));
        }

        return blog;
    }

    // Each subscription is newer than the one before, so the order is known.
    private async Task FollowAsync(Subsite follower, Subsite blog)
    {
        await api.SaveAsync<BlogsDbContext>(Subscription.Create(follower.OwnerAccountId!.Value, blog.Id, api.Time.GetUtcNow()));
        api.Time.Advance(TimeSpan.FromSeconds(1));
    }

    private static JsonElement[] Items(JsonElement page) => [.. page.GetProperty("items").EnumerateArray()];

    private static Guid[] Ids(JsonElement page) => [.. Items(page).Select(c => c.GetProperty("id").GetGuid())];

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

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
