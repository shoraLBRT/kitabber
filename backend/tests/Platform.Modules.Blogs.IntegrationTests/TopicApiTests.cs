using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Seeding;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>The Api host with the Blogs module and its demo seeder, on PostgreSQL and Redis in Testcontainers.</summary>
public sealed class TopicsApi : IntegrationApiFactory
{
    public TopicsApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(services => services.AddDemoSeeder<BlogsDemoSeeder>());
}

/// <summary>E05-09: the list of topics the editor offers to publish in (ADR-0046), and the demo topics.</summary>
public sealed class TopicApiTests(TopicsApi api) : IClassFixture<TopicsApi>, IAsyncLifetime
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
    public async Task AnyoneListsTheTopicsByNameWithoutPersonalBlogs()
    {
        var now = api.Time.GetUtcNow();
        await api.SaveAsync<BlogsDbContext>(
            Subsite.CreateTopic("Наука", "science", Guid.CreateVersion7(now), now),
            Subsite.CreateTopic("Игры", "games", Guid.CreateVersion7(now), now.AddSeconds(1)),
            Subsite.CreatePersonal(Guid.CreateVersion7(now), now));

        var topics = await ListAsync();

        Assert.Equal(["Игры", "Наука"], topics.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal(["games", "science"], topics.Select(t => t.GetProperty("nickname").GetString()));
    }

    [Fact]
    public async Task NoTopicsIsAnEmptyList() => Assert.Empty(await ListAsync());

    [Fact]
    public async Task DemoTopicsAreSeededOnce()
    {
        await api.SeedDemoAsync();
        await api.SeedDemoAsync();

        var topics = await ListAsync();

        Assert.Equal(TopicFactory.Topics.Count, topics.Length);
        Assert.Equal(api.Data.Id("topic", 0), topics.Single(t => t.GetProperty("nickname").GetString() == "kino").GetProperty("id").GetGuid());
    }

    // E11-01, ADR-0082: the topic page reads the topic by its address and shows who owns it.
    [Fact]
    public async Task TopicProfileNamesItsOwnersBlog()
    {
        var now = api.Time.GetUtcNow();
        var owner = Guid.CreateVersion7(now);
        var ownersBlog = Subsite.CreatePersonal(owner, now);
        ownersBlog.Rename("Анна");
        ownersBlog.SetNickname($"anna{owner.ToString("N")[..8]}");
        var topic = Subsite.CreateTopic("Наука", $"science{owner.ToString("N")[..8]}", owner, now);
        topic.Describe("Открытия и исследования");
        await api.SaveAsync<BlogsDbContext>(ownersBlog, topic);

        var profile = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/profiles/by-nickname/{topic.Nickname}", Ct);

        Assert.Equal("topic", profile.GetProperty("type").GetString());
        Assert.Equal("Открытия и исследования", profile.GetProperty("description").GetString());
        var shown = profile.GetProperty("owner");
        Assert.Equal(ownersBlog.Id, shown.GetProperty("id").GetGuid());
        Assert.Equal("Анна", shown.GetProperty("name").GetString());
        Assert.Equal(ownersBlog.Nickname, shown.GetProperty("nickname").GetString());
        // A guest is not the owner.
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("registeredAt").ValueKind);
    }

    [Fact]
    public async Task TopicWhoseOwnerHasNoBlogYetAndAPersonalBlogNameNoOwner()
    {
        var now = api.Time.GetUtcNow();
        var topic = Subsite.CreateTopic("Игры", $"games{now.Ticks % 100000}", Guid.CreateVersion7(now), now);
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(now), now);
        await api.SaveAsync<BlogsDbContext>(topic, blog);

        var topicProfile = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/profiles/{topic.Id}", Ct);
        var blogProfile = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/profiles/{blog.Id}", Ct);

        Assert.Equal(JsonValueKind.Null, topicProfile.GetProperty("owner").ValueKind);
        Assert.Equal(JsonValueKind.Null, blogProfile.GetProperty("owner").ValueKind);
    }

    [Fact]
    public async Task EveryDemoTopicIsOwnedByADemoAccount()
    {
        await api.SeedDemoAsync();

        await using var scope = api.Services.CreateAsyncScope();
        var topics = await scope.ServiceProvider.GetRequiredService<BlogsDbContext>().Subsites
            .Where(s => s.Type == SubsiteType.Topic).ToListAsync(Ct);

        Assert.Equal(TopicFactory.Topics.Count, topics.Count);
        Assert.All(topics, t => Assert.Equal(api.Data.Id("account", NumberOf(t)), t.OwnerAccountId));
    }

    private int NumberOf(Subsite topic) =>
        Enumerable.Range(0, TopicFactory.Topics.Count).Single(n => api.Data.Id("topic", n) == topic.Id);

    private async Task<JsonElement[]> ListAsync()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/topics", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray()];
    }
}
