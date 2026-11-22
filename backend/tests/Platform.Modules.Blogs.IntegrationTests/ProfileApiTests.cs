using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>The Api host with the Blogs module, on PostgreSQL and Redis in Testcontainers, signed in by the test scheme.</summary>
public sealed class ProfilesApi : IntegrationApiFactory
{
    public ProfilesApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(services => services.AddTestAuthentication());
}

/// <summary>E04-01: the profile API — by id and by nickname for everyone, the own profile for the signed-in account.</summary>
public sealed class ProfileApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
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
    public async Task AnyoneReadsAProfileByIdAndByNickname()
    {
        var blog = await SaveDemoBlogAsync();

        var byId = await GetAsync($"/api/v1/profiles/{blog.Id}");
        var byNickname = await GetAsync("/api/v1/profiles/by-nickname/@DEMO0");

        foreach (var profile in new[] { byId, byNickname })
        {
            Assert.Equal(blog.Id, profile.GetProperty("id").GetGuid());
            Assert.Equal("personal", profile.GetProperty("type").GetString());
            Assert.Equal(blog.Name, profile.GetProperty("name").GetString());
            Assert.Equal("demo0", profile.GetProperty("nickname").GetString());
            Assert.Equal(blog.Description, profile.GetProperty("description").GetString());
            // Only the owner sees when the blog's owner registered.
            Assert.Equal(JsonValueKind.Null, profile.GetProperty("registeredAt").ValueKind);
        }
    }

    // The author of a post (E07-04, ADR-0050): the post knows only the account, the header shows its personal blog.
    [Fact]
    public async Task PersonalBlogIsFoundByItsOwnersAccount()
    {
        var blog = await SaveDemoBlogAsync();

        var profile = await GetAsync($"/api/v1/profiles/by-account/{blog.OwnerAccountId}");

        Assert.Equal(blog.Id, profile.GetProperty("id").GetGuid());
        Assert.Equal("personal", profile.GetProperty("type").GetString());
        Assert.Equal(blog.Name, profile.GetProperty("name").GetString());
    }

    [Fact]
    public async Task AccountWithoutABlogIsNotFoundAndGetsNone()
    {
        await SaveDemoBlogAsync();
        var stranger = Guid.CreateVersion7(api.Time.GetUtcNow());

        using var response = await _client.GetAsync($"/api/v1/profiles/by-account/{stranger}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("profile_not_found", await CodeOf(response));
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        Assert.False(await db.Subsites.AnyAsync(s => s.OwnerAccountId == stranger, Ct));
    }

    [Fact]
    public async Task OwnerSeesTheRegistrationDateAndOthersDoNot()
    {
        var blog = await SaveDemoBlogAsync();

        var asOwner = await GetAsync($"/api/v1/profiles/{blog.Id}", blog.OwnerAccountId);
        var asStranger = await GetAsync($"/api/v1/profiles/{blog.Id}", Guid.CreateVersion7(api.Time.GetUtcNow()));

        Assert.Equal(blog.CreatedAt, asOwner.GetProperty("registeredAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, asStranger.GetProperty("registeredAt").ValueKind);
    }

    [Theory]
    [InlineData("/api/v1/profiles/0199a1b2-0000-7000-8000-000000000000")]
    [InlineData("/api/v1/profiles/by-nickname/nobody")]
    public async Task UnknownProfileIsNotFound(string path)
    {
        await SaveDemoBlogAsync();

        using var response = await _client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("profile_not_found", await CodeOf(response));
    }

    [Theory]
    [InlineData("a-b")]
    [InlineData("id42")]
    [InlineData("ab")]
    public async Task MalformedNicknameIsAValidationError(string nickname)
    {
        using var response = await _client.GetAsync($"/api/v1/profiles/by-nickname/{nickname}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await CodeOf(response));
    }

    [Fact]
    public async Task OwnProfileNeedsSignIn()
    {
        using var response = await _client.GetAsync("/api/v1/profiles/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwnProfileIsThePersonalBlogTheWorkersCreated()
    {
        var blog = await SaveDemoBlogAsync();

        var own = await GetAsync("/api/v1/profiles/me", blog.OwnerAccountId);

        Assert.Equal(blog.Id, own.GetProperty("id").GetGuid());
        Assert.Equal(blog.CreatedAt, own.GetProperty("registeredAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task AccountWithoutABlogYetGetsItsPersonalBlogOnce()
    {
        // An account activated before blogs existed, or before the workers handled its event.
        var account = Guid.CreateVersion7(api.Time.GetUtcNow());

        var first = await GetAsync("/api/v1/profiles/me", account);
        api.Time.Advance(TimeSpan.FromMinutes(1));
        var second = await GetAsync("/api/v1/profiles/me", account);

        Assert.Equal("personal", first.GetProperty("type").GetString());
        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal(api.Time.GetUtcNow() - TimeSpan.FromMinutes(1), first.GetProperty("registeredAt").GetDateTimeOffset());
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        Assert.Equal(1, await db.Subsites.CountAsync(s => s.OwnerAccountId == account, Ct));
    }

    [Fact]
    public async Task DatabaseKeepsOnePersonalBlogPerAccountAndOneBlogPerNickname()
    {
        var blog = await SaveDemoBlogAsync();
        var twin = Subsite.CreatePersonal(blog.OwnerAccountId!.Value, api.Time.GetUtcNow());
        var namesake = Subsite.CreatePersonal(Guid.CreateVersion7(api.Time.GetUtcNow()), api.Time.GetUtcNow());
        namesake.SetNickname("demo0");

        await Assert.ThrowsAsync<DbUpdateException>(() => api.SaveAsync<BlogsDbContext>(twin));
        await Assert.ThrowsAsync<DbUpdateException>(() => api.SaveAsync<BlogsDbContext>(namesake));
    }

    private async Task<Subsite> SaveDemoBlogAsync()
    {
        var blog = new PersonalBlogFactory(api.Data).Build();
        await api.SaveAsync<BlogsDbContext>(blog);
        return blog;
    }

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

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
