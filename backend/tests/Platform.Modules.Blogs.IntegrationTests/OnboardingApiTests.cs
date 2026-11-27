using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Modules.Blogs.Profiles;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E03-10, ADR-0102: the onboarding after sign-up through <c>POST /api/v1/profiles/me/onboarding</c> — the name (2–25
/// characters), the avatar and the topics that become subscriptions; every step may be skipped, and it happens once.
/// </summary>
public sealed class OnboardingApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
{
    private const string _path = "/api/v1/profiles/me/onboarding";

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
    public async Task NewAccountIsOfferedTheOnboardingAndFinishesItOnce()
    {
        var account = NewAccount();
        var (science, _) = await TopicAsync("Наука");
        var (books, _) = await TopicAsync("Книги");
        var avatar = await UploadAsync(account);

        var before = await GetOwnAsync(account);
        Assert.True(before.GetProperty("onboardingPending").GetBoolean());

        using var response = await PostAsync(
            account, new { name = "  Ян  ", avatarId = avatar, topicIds = new[] { science.Id, books.Id, science.Id } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Ян", saved.GetProperty("name").GetString());
        Assert.Equal(avatar, saved.GetProperty("avatarId").GetGuid());
        Assert.Equal(2, saved.GetProperty("subscriptionCount").GetInt32());
        Assert.False(saved.GetProperty("onboardingPending").GetBoolean());
        // The chosen topics are subscriptions like any other.
        foreach (var topic in new[] { science, books })
        {
            var seen = await GetAsync($"/api/v1/profiles/{topic.Id}", account);
            Assert.True(seen.GetProperty("subscribed").GetBoolean());
            Assert.Equal(1, seen.GetProperty("subscriberCount").GetInt32());
        }

        Assert.False((await GetOwnAsync(account)).GetProperty("onboardingPending").GetBoolean());

        // It is never offered nor taken again.
        using var again = await PostAsync(account, new { name = "Другое имя" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var problem = await again.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(OnboardingEndpoints.OnboardingDoneCode, problem.GetProperty("code").GetString());
        Assert.Equal("Ян", (await GetOwnAsync(account)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task SkippingEveryStepOnlyMarksItDone()
    {
        var account = NewAccount();

        using var response = await PostAsync(account, new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var own = await GetOwnAsync(account);
        Assert.False(own.GetProperty("onboardingPending").GetBoolean());
        Assert.Equal(JsonValueKind.Null, own.GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Null, own.GetProperty("avatarId").ValueKind);
        Assert.Equal(0, own.GetProperty("subscriptionCount").GetInt32());
    }

    [Fact]
    public async Task SkippedStepsLeaveTheBlogAsItIs()
    {
        var account = NewAccount();
        (await PutProfileAsync(account, new { name = "Анна", description = "Пишу о книгах" })).Dispose();

        using var response = await PostAsync(account, new { name = " " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var own = await GetOwnAsync(account);
        Assert.Equal("Анна", own.GetProperty("name").GetString());
        Assert.Equal("Пишу о книгах", own.GetProperty("description").GetString());
    }

    [Fact]
    public async Task WrongChoicesAreRefusedAndNothingIsSaved()
    {
        var account = NewAccount();
        var (topic, _) = await TopicAsync("Наука");
        var ownBlog = (await GetOwnAsync(account)).GetProperty("id").GetGuid();
        var strangersImage = await UploadAsync(NewAccount());

        foreach (var (body, field) in new (object Body, string Field)[]
                 {
                     (new { name = "Я" }, "name"),
                     (new { name = new string('я', 26) }, "name"),
                     (new { avatarId = strangersImage }, "avatarId"),
                     (new { topicIds = new[] { topic.Id, Guid.CreateVersion7(api.Time.GetUtcNow()) } }, "topicIds"),
                     (new { topicIds = new[] { ownBlog } }, "topicIds"),
                     (new { topicIds = Enumerable.Range(0, OnboardingEndpoints.MaxTopics + 1).Select(_ => Guid.NewGuid()) }, "topicIds"),
                 })
        {
            using var response = await PostAsync(account, body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
            Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), field);
        }

        var own = await GetOwnAsync(account);
        Assert.True(own.GetProperty("onboardingPending").GetBoolean());
        Assert.Equal(0, own.GetProperty("subscriptionCount").GetInt32());
    }

    [Fact]
    public async Task AccountsFromBeforeTheOnboardingAndOthersNeverSeeIt()
    {
        var blog = new PersonalBlogFactory(api.Data).Build();
        await api.SaveAsync<BlogsDbContext>(blog);
        var stranger = NewAccount();

        Assert.False((await GetOwnAsync(blog.OwnerAccountId!.Value)).GetProperty("onboardingPending").GetBoolean());
        // Someone else's pending onboarding is not shown to a reader.
        var newcomer = NewAccount();
        var newcomersBlog = (await GetOwnAsync(newcomer)).GetProperty("id").GetGuid();
        Assert.False((await GetAsync($"/api/v1/profiles/{newcomersBlog}", stranger)).GetProperty("onboardingPending").GetBoolean());
        Assert.False((await GetAsync($"/api/v1/profiles/{newcomersBlog}")).GetProperty("onboardingPending").GetBoolean());
    }

    [Fact]
    public async Task OnboardingNeedsSignIn()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _path) { Content = JsonContent.Create(new { }) };

        using var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Guid NewAccount() => Guid.CreateVersion7(api.Time.GetUtcNow());

    private async Task<(Subsite Topic, Guid Owner)> TopicAsync(string name)
    {
        var owner = NewAccount();
        var topic = Subsite.CreateTopic(name, $"onb{owner.ToString("N")[^12..]}", owner, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        return (topic, owner);
    }

    private async Task<Guid> UploadAsync(Guid account)
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, .. "IHDR"u8, .. new byte[240]];
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media")
        {
            Content = new MultipartFormDataContent { { file, "file", "avatar.png" } },
        };
        request.As(account.ToString("D"));

        using var response = await _client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> PostAsync(Guid account, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _path) { Content = JsonContent.Create(body) };
        request.As(account.ToString("D"));
        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> PutProfileAsync(Guid account, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me") { Content = JsonContent.Create(body) };
        request.As(account.ToString("D"));
        return await _client.SendAsync(request, Ct);
    }

    private Task<JsonElement> GetOwnAsync(Guid account) => GetAsync("/api/v1/profiles/me", account);

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
