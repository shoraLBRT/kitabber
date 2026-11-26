using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Modules.Blogs.Topics;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E11-02 (FR-TOPIC, FR-SUB, ADR-0088): the topic catalogue — the most followed first, searched by name or address, page
/// by page — and the menu's topics: the person's subscriptions, or the popular ones for a guest.
/// </summary>
public sealed class TopicCatalogApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
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
    public async Task TheCatalogListsTopicsMostFollowedFirstWithoutPersonalBlogs()
    {
        var science = await TopicAsync("Наука", "science", followers: 1);
        var games = await TopicAsync("Игры", "games", followers: 3);
        var cinema = await TopicAsync("Кино", "cinema", followers: 0);
        var person = Subsite.CreatePersonal(NewAccount(), api.Time.GetUtcNow());
        person.Rename("Наука и жизнь");
        await api.SaveAsync<BlogsDbContext>(person);

        var page = await GetAsync("/api/v1/topics/catalog");

        Assert.Equal([games.Id, science.Id, cinema.Id], Ids(page));
        Assert.Equal([3, 1, 0], Items(page).Select(t => t.GetProperty("subscriberCount").GetInt32()));
        Assert.Equal("games", Items(page)[0].GetProperty("nickname").GetString());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task TheCatalogIsSearchedByNameOrAddressInAnyCase()
    {
        var science = await TopicAsync("Наука", "science", followers: 0);
        var popularScience = await TopicAsync("Научпоп", "popsci", followers: 2);
        await TopicAsync("Игры", "games", followers: 5);

        var byName = await GetAsync("/api/v1/topics/catalog?q=" + Uri.EscapeDataString("  НАУ "));
        var byAddress = await GetAsync("/api/v1/topics/catalog?q=SCI");
        var nothing = await GetAsync("/api/v1/topics/catalog?q=" + Uri.EscapeDataString("%"));

        Assert.Equal([popularScience.Id, science.Id], Ids(byName));
        Assert.Equal([popularScience.Id, science.Id], Ids(byAddress));
        Assert.Empty(Items(nothing));
    }

    [Fact]
    public async Task TheCatalogGoesPageByPageWithoutLosingTopicsOfEqualFollowers()
    {
        var topics = new List<Subsite>();
        for (var i = 0; i < 5; i++)
        {
            topics.Add(await TopicAsync($"Тема {i}", $"page{i}", followers: i < 2 ? 1 : 0));
        }

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await GetAsync("/api/v1/topics/catalog?limit=2" + (cursor is null ? "" : $"&cursor={cursor}"));
            seen.AddRange(Ids(page));
            cursor = page.GetProperty("nextCursor").GetString();
            pages++;
        }
        while (cursor is not null);

        Assert.Equal(3, pages);
        Assert.Equal(5, seen.Distinct().Count());
        Assert.Equal([topics[1].Id, topics[0].Id], seen.Take(2));
    }

    [Fact]
    public async Task ATooLongSearchOrABadPageIsRefused()
    {
        var search = await _client.GetAsync("/api/v1/topics/catalog?q=" + new string('а', TopicCatalogRules.QueryMaxLength + 1), Ct);
        var limit = await _client.GetAsync("/api/v1/topics/catalog?limit=0", Ct);
        var cursor = await _client.GetAsync("/api/v1/topics/catalog?cursor=broken", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, search.StatusCode);
        var problem = await search.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("q", out _));
        Assert.Equal(HttpStatusCode.BadRequest, limit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cursor.StatusCode);
    }

    [Fact]
    public async Task AGuestsMenuShowsThePopularTopics()
    {
        var quiet = await TopicAsync("Тихая", "quiet", followers: 0);
        var loud = await TopicAsync("Громкая", "loud", followers: 2);

        var menu = await GetAsync("/api/v1/topics/menu");

        Assert.Equal(TopicMenuSources.Popular, menu.GetProperty("source").GetString());
        Assert.Equal([loud.Id, quiet.Id], menu.GetProperty("topics").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task ThePopularMenuIsShort()
    {
        for (var i = 0; i <= TopicCatalogRules.MenuSize; i++)
        {
            await TopicAsync($"Тема {i}", $"many{i}", followers: 0);
        }

        var menu = await GetAsync("/api/v1/topics/menu");

        Assert.Equal(TopicCatalogRules.MenuSize, menu.GetProperty("topics").GetArrayLength());
    }

    [Fact]
    public async Task APersonsMenuShowsTheirSubscribedTopicsByName()
    {
        var reader = NewAccount();
        var science = await TopicAsync("Наука", "science", followers: 0);
        var art = await TopicAsync("Арт", "art", followers: 0);
        await TopicAsync("Игры", "games", followers: 4);
        var blog = Subsite.CreatePersonal(NewAccount(), api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(blog);
        var now = api.Time.GetUtcNow();
        await api.SaveAsync<BlogsDbContext>(
            Subscription.Create(reader, science.Id, now),
            Subscription.Create(reader, art.Id, now),
            Subscription.Create(reader, blog.Id, now));

        var menu = await GetAsync("/api/v1/topics/menu", reader);

        Assert.Equal(TopicMenuSources.Subscriptions, menu.GetProperty("source").GetString());
        Assert.Equal(["Арт", "Наука"], menu.GetProperty("topics").EnumerateArray().Select(t => t.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task APersonWithoutSubscribedTopicsSeesThePopularOnes()
    {
        var games = await TopicAsync("Игры", "games", followers: 1);

        var menu = await GetAsync("/api/v1/topics/menu", NewAccount());

        Assert.Equal(TopicMenuSources.Popular, menu.GetProperty("source").GetString());
        Assert.Equal([games.Id], menu.GetProperty("topics").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()));
    }

    private Guid NewAccount() => Guid.CreateVersion7(api.Time.GetUtcNow());

    private async Task<Subsite> TopicAsync(string name, string nickname, int followers)
    {
        var now = api.Time.GetUtcNow();
        var topic = Subsite.CreateTopic(name, nickname, NewAccount(), now);
        topic.Describe($"О теме «{name}»");
        await api.SaveAsync<BlogsDbContext>(topic);
        for (var i = 0; i < followers; i++)
        {
            await api.SaveAsync<BlogsDbContext>(Subscription.Create(NewAccount(), topic.Id, now));
        }

        // Each topic is newer than the one before, so equal numbers keep a known order.
        api.Time.Advance(TimeSpan.FromSeconds(1));
        return topic;
    }

    private static JsonElement[] Items(JsonElement page) => [.. page.GetProperty("items").EnumerateArray()];

    private static Guid[] Ids(JsonElement page) => [.. Items(page).Select(t => t.GetProperty("id").GetGuid())];

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

/// <summary>The rules of the topic catalogue apart from the database (ADR-0088).</summary>
public sealed class TopicCatalogRulesTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  наука  и   жизнь ", "наука и жизнь")]
    public void TheSearchIsTrimmedAndItsSpacesCollapsed(string? input, string? expected) =>
        Assert.Equal(expected, TopicCatalogRules.NormalizeQuery(input));

    [Fact]
    public void ALongSearchIsInvalid()
    {
        Assert.True(TopicCatalogRules.IsValidQuery(null));
        Assert.True(TopicCatalogRules.IsValidQuery(new string('а', TopicCatalogRules.QueryMaxLength)));
        Assert.False(TopicCatalogRules.IsValidQuery(new string('а', TopicCatalogRules.QueryMaxLength + 1)));
    }

    [Fact]
    public void LikeWildcardsInTheSearchMatchThemselves() =>
        Assert.Equal(@"%50\%\_off\\%", TopicCatalogRules.ContainsPattern(@"50%_off\"));
}
