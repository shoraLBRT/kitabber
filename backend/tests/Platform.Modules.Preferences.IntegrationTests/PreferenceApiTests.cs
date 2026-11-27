using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Preferences.IntegrationTests;

/// <summary>The Api host on PostgreSQL and Redis in Testcontainers, signed in by the test scheme.</summary>
public sealed class PreferencesApi : IntegrationApiFactory
{
    public PreferencesApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(services => services.AddTestAuthentication());
}

/// <summary>
/// E04-08: the settings of the signed-in account through <c>/api/v1/preferences/me</c> — the defaults before any
/// change, a change kept on the server and given to every device, a refused value, and no access for a guest or to
/// another account's settings.
/// </summary>
public sealed class PreferenceApiTests(PreferencesApi api) : IClassFixture<PreferencesApi>, IAsyncLifetime
{
    private const string _own = "/api/v1/preferences/me";
    private const string _general = "/api/v1/preferences/me/general";
    private const string _feeds = "/api/v1/preferences/me/feeds";
    private readonly Guid _anna = Guid.CreateVersion7();
    private readonly Guid _boris = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AccountThatChangedNothingGetsTheDefaults()
    {
        using var phone = api.CreateClient();

        using var response = await SendAsync(phone, HttpMethod.Get, _own, _anna);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("system", await ThemeOf(response));
        Assert.Equal(0, await RowsAsync());
    }

    [Fact]
    public async Task ChoiceMadeOnOneDeviceIsWhatAnotherDeviceGets()
    {
        using var laptop = api.CreateClient();
        using var phone = api.CreateClient();

        using var saved = await SendAsync(laptop, HttpMethod.Put, _general, _anna, new { theme = "dark" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("dark", await ThemeOf(saved));

        using var read = await SendAsync(phone, HttpMethod.Get, _own, _anna);
        Assert.Equal("dark", await ThemeOf(read));

        using var again = await SendAsync(phone, HttpMethod.Put, _general, _anna, new { theme = "light" });
        using var back = await SendAsync(laptop, HttpMethod.Get, _own, _anna);
        Assert.Equal("light", await ThemeOf(back));
        Assert.Equal(1, await RowsAsync());

        await using var scope = api.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<PreferencesDbContext>()
            .AccountPreferences.SingleAsync(Ct);
        Assert.Equal(api.Time.GetUtcNow(), row.UpdatedAt);
    }

    [Fact]
    public async Task OneAccountsChoiceIsNotAnothers()
    {
        using var client = api.CreateClient();

        using var saved = await SendAsync(client, HttpMethod.Put, _general, _anna, new { theme = "dark" });
        using var other = await SendAsync(client, HttpMethod.Get, _own, _boris);

        Assert.Equal("system", await ThemeOf(other));
    }

    [Theory]
    [InlineData("sepia")]
    [InlineData(null)]
    public async Task UnknownThemeIsAValidationError(string? theme)
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Put, _general, _anna, new { theme });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("theme", out _));
        Assert.Equal(0, await RowsAsync());
    }

    [Fact]
    public async Task FreshFeedThresholdIsRememberedForEveryDevice()
    {
        using var laptop = api.CreateClient();
        using var phone = api.CreateClient();

        using var initial = await SendAsync(phone, HttpMethod.Get, _own, _anna);
        Assert.Equal(JsonValueKind.Null, (await FreshMinRatingOf(initial)).ValueKind);

        using var saved = await SendAsync(laptop, HttpMethod.Put, _feeds, _anna, new { freshMinRating = 5 });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(5, (await FreshMinRatingOf(saved)).GetInt32());

        using var read = await SendAsync(phone, HttpMethod.Get, _own, _anna);
        var body = await read.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(5, body.GetProperty("feeds").GetProperty("freshMinRating").GetInt32());
        Assert.Equal("system", body.GetProperty("general").GetProperty("theme").GetString());

        // The theme saved later keeps the threshold; «все» clears it.
        using var theme = await SendAsync(phone, HttpMethod.Put, _general, _anna, new { theme = "dark" });
        Assert.Equal(5, (await FreshMinRatingOf(theme)).GetInt32());
        using var all = await SendAsync(phone, HttpMethod.Put, _feeds, _anna, new { freshMinRating = (int?)null });
        Assert.Equal(JsonValueKind.Null, (await FreshMinRatingOf(all)).ValueKind);
        using var back = await SendAsync(laptop, HttpMethod.Get, _own, _anna);
        Assert.Equal("dark", await ThemeOf(back));
        Assert.Equal(1, await RowsAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-5)]
    public async Task UnknownFreshFeedThresholdIsAValidationError(int threshold)
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Put, _feeds, _anna, new { freshMinRating = threshold });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("freshMinRating", out _));
        Assert.Equal(0, await RowsAsync());
    }

    [Fact]
    public async Task HomeFeedAndAutoplayAreRememberedForEveryDevice()
    {
        using var laptop = api.CreateClient();
        using var phone = api.CreateClient();

        using var initial = await SendAsync(phone, HttpMethod.Get, _own, _anna);
        var feeds = await FeedsOf(initial);
        Assert.Equal("popular", feeds.GetProperty("homeFeed").GetString());
        Assert.True(feeds.GetProperty("autoplayVideo").GetBoolean());

        using var saved = await SendAsync(
            laptop, HttpMethod.Put, _feeds, _anna, new { freshMinRating = 5, homeFeed = "fresh", autoplayVideo = false });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var read = await SendAsync(phone, HttpMethod.Get, _own, _anna);
        feeds = await FeedsOf(read);
        Assert.Equal("fresh", feeds.GetProperty("homeFeed").GetString());
        Assert.False(feeds.GetProperty("autoplayVideo").GetBoolean());
        Assert.Equal(5, feeds.GetProperty("freshMinRating").GetInt32());

        // The feed's own filter sends only the threshold: the home feed and the autoplay stay.
        using var filter = await SendAsync(phone, HttpMethod.Put, _feeds, _anna, new { freshMinRating = 10 });
        feeds = await FeedsOf(filter);
        Assert.Equal(10, feeds.GetProperty("freshMinRating").GetInt32());
        Assert.Equal("fresh", feeds.GetProperty("homeFeed").GetString());
        Assert.False(feeds.GetProperty("autoplayVideo").GetBoolean());

        using var my = await SendAsync(phone, HttpMethod.Put, _feeds, _anna, new { freshMinRating = 10, homeFeed = "my" });
        Assert.Equal("my", (await FeedsOf(my)).GetProperty("homeFeed").GetString());
        Assert.Equal(1, await RowsAsync());
    }

    [Fact]
    public async Task UnknownHomeFeedIsAValidationError()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(
            client, HttpMethod.Put, _feeds, _anna, new { freshMinRating = (int?)null, homeFeed = "best" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("homeFeed", out _));
        Assert.Equal(0, await RowsAsync());
    }

    [Fact]
    public async Task GuestHasNoFeedSettings()
    {
        using var client = api.CreateClient();

        using var write = await SendAsync(
            client, HttpMethod.Put, _feeds, null, new { freshMinRating = 5, homeFeed = "fresh", autoplayVideo = false });

        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        Assert.Equal(0, await RowsAsync());
    }

    [Fact]
    public async Task GuestHasNoSettings()
    {
        using var client = api.CreateClient();

        using var read = await SendAsync(client, HttpMethod.Get, _own, null);
        using var write = await SendAsync(client, HttpMethod.Put, _general, null, new { theme = "dark" });

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        Assert.Equal(0, await RowsAsync());
    }

    private async Task<int> RowsAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PreferencesDbContext>().AccountPreferences.CountAsync(Ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, Guid? account, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task<string?> ThemeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("general").GetProperty("theme").GetString();

    private static async Task<JsonElement> FeedsOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("feeds");

    private static async Task<JsonElement> FreshMinRatingOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("feeds").GetProperty("freshMinRating");
}
