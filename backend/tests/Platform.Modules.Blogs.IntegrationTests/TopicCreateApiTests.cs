using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Modules.Blogs.Topics;
using Platform.Modules.Identity;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>The Api host with at most <see cref="MaxTopics"/> topics per account, on PostgreSQL and Redis in Testcontainers.</summary>
public sealed class TopicCreateApi : IntegrationApiFactory
{
    public const int MaxTopics = 2;

    public TopicCreateApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
        WithSetting(BlogsSettings.MaxTopicsPerAccountKey, MaxTopics.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WithSetting(BlogsSettings.StopWordsKey, "official,официальн");
    }
}

/// <summary>E11-11, ADR-0084: a person creates a topic and owns it; duplicates, forbidden names and the limit are refused.</summary>
public sealed class TopicCreateApiTests(TopicCreateApi api) : IClassFixture<TopicCreateApi>, IAsyncLifetime
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
    public async Task AConfirmedAccountCreatesATopicAndOwnsIt()
    {
        var owner = await SaveConfirmedAccountAsync(1, "topic-owner@example.com");

        using var response = await CreateAsync(owner, new { name = " Наука ", nickname = "Science_Club", description = "Открытия" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Наука", created.GetProperty("name").GetString());
        Assert.Equal("science_club", created.GetProperty("nickname").GetString());
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/profiles/{id}", response.Headers.Location?.ToString());

        await using var scope = api.Services.CreateAsyncScope();
        var topic = await scope.ServiceProvider.GetRequiredService<BlogsDbContext>().Subsites.SingleAsync(s => s.Id == id, Ct);
        Assert.Equal(SubsiteType.Topic, topic.Type);
        Assert.Equal(owner, topic.OwnerAccountId);
        Assert.Equal("Открытия", topic.Description);
        Assert.Equal(api.Time.GetUtcNow(), topic.CreatedAt);
        var profile = await _client.GetFromJsonAsync<JsonElement>("/api/v1/profiles/by-nickname/science_club", Ct);
        Assert.Equal("topic", profile.GetProperty("type").GetString());
    }

    [Fact]
    public async Task OnlyASignedInAccountWithAConfirmedEmailCreatesATopic()
    {
        using var anonymous = await CreateAsync(account: null, new { name = "Наука", nickname = "science" });
        using var unconfirmed = await CreateAsync(Guid.CreateVersion7(api.Time.GetUtcNow()), new { name = "Наука", nickname = "science" });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unconfirmed.StatusCode);
        Assert.Equal(AccountPolicies.EmailNotConfirmedCode, await CodeOf(unconfirmed));
        Assert.Equal(0, await CountTopicsAsync());
    }

    [Fact]
    public async Task ATakenNameOrAddressIsRefused()
    {
        var now = api.Time.GetUtcNow();
        var other = Guid.CreateVersion7(now);
        var blog = Subsite.CreatePersonal(other, now);
        blog.SetNickname("anna_blog");
        await api.SaveAsync<BlogsDbContext>(Subsite.CreateTopic("Игры", "games", other, now), blog);
        var owner = await SaveConfirmedAccountAsync(2, "duplicates@example.com");

        using var sameName = await CreateAsync(owner, new { name = "ИГРЫ", nickname = "games_two" });
        using var sameTopicAddress = await CreateAsync(owner, new { name = "Игры 2", nickname = "GAMES" });
        using var nicknameOfABlog = await CreateAsync(owner, new { name = "Анна", nickname = "anna_blog" });

        Assert.Equal(["name"], await ErrorFieldsOf(sameName));
        Assert.Equal(["nickname"], await ErrorFieldsOf(sameTopicAddress));
        Assert.Equal(["nickname"], await ErrorFieldsOf(nicknameOfABlog));
        Assert.Equal(1, await CountTopicsAsync());
    }

    [Theory]
    [InlineData("Официальный клуб", "club", "name")]
    [InlineData("Клуб", "official_club", "nickname")]
    [InlineData("Настройки", "settings", "nickname")]
    [InlineData("Клуб", "id42", "nickname")]
    [InlineData("Клуб", "", "nickname")]
    [InlineData("Я", "club", "name")]
    [InlineData("", "club", "name")]
    [InlineData("Название длиннее двадцати пяти", "club", "name")]
    public async Task AForbiddenOrWrongNameOrAddressIsRefused(string name, string nickname, string field)
    {
        var owner = await SaveConfirmedAccountAsync(3, "forbidden@example.com");

        using var response = await CreateAsync(owner, new { name, nickname });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([field], await ErrorFieldsOf(response));
        Assert.Equal(0, await CountTopicsAsync());
    }

    [Fact]
    public async Task ATooLongDescriptionIsRefused()
    {
        var owner = await SaveConfirmedAccountAsync(4, "description@example.com");

        using var response = await CreateAsync(owner, new { name = "Клуб", nickname = "club", description = new string('а', 161) });

        Assert.Equal(["description"], await ErrorFieldsOf(response));
    }

    [Fact]
    public async Task AnAccountCreatesNoMoreTopicsThanTheSettingAllows()
    {
        var owner = await SaveConfirmedAccountAsync(5, "limit@example.com");
        var other = await SaveConfirmedAccountAsync(6, "limit-other@example.com");
        for (var i = 0; i < TopicCreateApi.MaxTopics; i++)
        {
            using var created = await CreateAsync(owner, new { name = $"Тема {i}", nickname = $"limit_topic_{i}" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        using var overLimit = await CreateAsync(owner, new { name = "Ещё тема", nickname = "limit_topic_more" });
        using var ofAnother = await CreateAsync(other, new { name = "Ещё тема", nickname = "limit_topic_more" });

        Assert.Equal(HttpStatusCode.Conflict, overLimit.StatusCode);
        Assert.Equal(TopicEndpoints.TopicLimitReachedCode, await CodeOf(overLimit));
        Assert.Equal(HttpStatusCode.Created, ofAnother.StatusCode);
        Assert.Equal(TopicCreateApi.MaxTopics + 1, await CountTopicsAsync());
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

    private async Task<HttpResponseMessage> CreateAsync(Guid? account, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/topics") { Content = JsonContent.Create(body) };
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<int> CountTopicsAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BlogsDbContext>().Subsites
            .CountAsync(s => s.Type == SubsiteType.Topic, Ct);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static async Task<string[]> ErrorFieldsOf(HttpResponseMessage response) =>
        [.. (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").EnumerateObject()
            .Select(e => e.Name).Order(StringComparer.Ordinal)];
}
