using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
            Subsite.CreateTopic("Наука", "science", now),
            Subsite.CreateTopic("Игры", "games", now.AddSeconds(1)),
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

    private async Task<JsonElement[]> ListAsync()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/topics", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray()];
    }
}
