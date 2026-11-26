using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs.Profiles;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E04-05, ADR-0090: the owner changes the nickname of the personal blog through <c>PUT /api/v1/profiles/me/nickname</c>;
/// a taken, reserved or lately given up nickname is refused, a change sooner than 30 days after the last one too, and the
/// old nickname leads to the blog.
/// </summary>
public sealed class ProfileNicknameApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
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
    public async Task OwnerSetsTheFirstNicknameAndChangesItOnceAndTheOldOneLeadsToTheBlog()
    {
        var blog = await SaveBlogAsync(nickname: null);
        var owner = blog.OwnerAccountId!.Value;

        using var first = await PutAsync(owner, "@Anna_First");
        using var second = await PutAsync(owner, "anna_second");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var saved = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("anna_second", saved.GetProperty("nickname").GetString());
        Assert.Equal(api.Time.GetUtcNow().AddDays(30), saved.GetProperty("nicknameChangeableFrom").GetDateTimeOffset());
        // The old nickname answers with the blog as it is now: the site leads to /u/anna_second.
        var byOld = await GetAsync("/api/v1/profiles/by-nickname/anna_first");
        Assert.Equal(blog.Id, byOld.GetProperty("id").GetGuid());
        Assert.Equal("anna_second", byOld.GetProperty("nickname").GetString());
        // A reader does not see when the owner may change it again.
        Assert.Equal(JsonValueKind.Null, byOld.GetProperty("nicknameChangeableFrom").ValueKind);
    }

    [Fact]
    public async Task ASecondChangeSoonerThanThirtyDaysIsRefused()
    {
        var blog = await SaveBlogAsync("anna_a");
        var owner = blog.OwnerAccountId!.Value;
        (await PutAsync(owner, "anna_b")).Dispose();

        api.Time.Advance(TimeSpan.FromDays(29));
        using var tooSoon = await PutAsync(owner, "anna_c");
        api.Time.Advance(TimeSpan.FromDays(1));
        using var inTime = await PutAsync(owner, "anna_c");

        Assert.Equal(HttpStatusCode.Conflict, tooSoon.StatusCode);
        Assert.Equal(ProfileEndpoints.NicknameChangeTooSoonCode, await CodeOf(tooSoon));
        Assert.Equal(HttpStatusCode.OK, inTime.StatusCode);
        Assert.Equal("anna_c", (await GetAsync($"/api/v1/profiles/{blog.Id}")).GetProperty("nickname").GetString());
    }

    [Fact]
    public async Task TheSameNicknameAgainIsNoChange()
    {
        var blog = await SaveBlogAsync("anna_same");
        var owner = blog.OwnerAccountId!.Value;
        (await PutAsync(owner, "anna_new")).Dispose();

        using var again = await PutAsync(owner, "ANNA_NEW");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task ANicknameOfAnotherBlogOrTopicIsRefused()
    {
        var now = api.Time.GetUtcNow();
        var other = await SaveBlogAsync("taken_blog");
        await api.SaveAsync<BlogsDbContext>(Subsite.CreateTopic("Наука", "taken_topic", Guid.CreateVersion7(now), now));
        var blog = await SaveBlogAsync("anna_free");

        using var blogTaken = await PutAsync(blog.OwnerAccountId!.Value, "Taken_Blog");
        using var topicTaken = await PutAsync(blog.OwnerAccountId!.Value, "taken_topic");

        Assert.Equal(HttpStatusCode.BadRequest, blogTaken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, topicTaken.StatusCode);
        Assert.Equal("Этот никнейм уже занят блогом или темой.", await NicknameErrorOf(blogTaken));
        Assert.Equal("taken_blog", (await GetAsync($"/api/v1/profiles/{other.Id}")).GetProperty("nickname").GetString());
    }

    [Theory]
    [InlineData("settings", "Такой никнейм выбрать нельзя.")]
    [InlineData("admin_anna", "Такой никнейм выбрать нельзя.")]
    [InlineData("id123", "Никнейм — от 3 до 32 латинских букв, цифр и _, начинается с буквы.")]
    [InlineData("", "Укажите никнейм.")]
    public async Task AReservedOrMalformedNicknameIsRefused(string nickname, string message)
    {
        var blog = await SaveBlogAsync($"anna_{Guid.NewGuid().ToString("N")[..8]}");

        using var response = await PutAsync(blog.OwnerAccountId!.Value, nickname);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, await NicknameErrorOf(response));
    }

    [Fact]
    public async Task AGivenUpNicknameIsHeldForNinetyDaysThenAnotherBlogTakesIt()
    {
        var first = await SaveBlogAsync("held_name");
        (await PutAsync(first.OwnerAccountId!.Value, "held_new")).Dispose();
        var second = await SaveBlogAsync("second_blog");

        using var held = await PutAsync(second.OwnerAccountId!.Value, "held_name");
        api.Time.Advance(TimeSpan.FromDays(90));
        using var free = await PutAsync(second.OwnerAccountId!.Value, "held_name");

        Assert.Equal(HttpStatusCode.BadRequest, held.StatusCode);
        Assert.Equal(HttpStatusCode.OK, free.StatusCode);
        // The address now names the second blog, and the first blog no longer keeps it.
        Assert.Equal(second.Id, (await GetAsync("/api/v1/profiles/by-nickname/held_name")).GetProperty("id").GetGuid());
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        Assert.False(await db.FormerNicknames.AnyAsync(f => f.Nickname == "held_name", Ct));
        Assert.True(await db.FormerNicknames.AnyAsync(f => f.Nickname == "second_blog" && f.SubsiteId == second.Id, Ct));
    }

    [Fact]
    public async Task TheOwnerTakesTheOwnOldNicknameBack()
    {
        var blog = await SaveBlogAsync("back_name");
        var owner = blog.OwnerAccountId!.Value;
        (await PutAsync(owner, "back_other")).Dispose();
        api.Time.Advance(TimeSpan.FromDays(30));

        using var back = await PutAsync(owner, "back_name");

        Assert.Equal(HttpStatusCode.OK, back.StatusCode);
        Assert.Equal(blog.Id, (await GetAsync("/api/v1/profiles/by-nickname/back_other")).GetProperty("id").GetGuid());
        Assert.Equal("back_name", (await GetAsync("/api/v1/profiles/by-nickname/back_name")).GetProperty("nickname").GetString());
    }

    [Fact]
    public async Task OnlyASignedInAccountChangesTheNickname()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/nickname")
        {
            Content = JsonContent.Create(new { nickname = "guest_name" }),
        };

        using var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Subsite> SaveBlogAsync(string? nickname)
    {
        var now = api.Time.GetUtcNow();
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(now), now);
        if (nickname is not null)
        {
            blog.SetNickname(nickname);
        }

        await api.SaveAsync<BlogsDbContext>(blog);
        return blog;
    }

    private async Task<HttpResponseMessage> PutAsync(Guid account, string nickname)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/nickname")
        {
            Content = JsonContent.Create(new { nickname }),
        };
        request.As(account.ToString("D"));
        return await _client.SendAsync(request, Ct);
    }

    private async Task<JsonElement> GetAsync(string path) => await _client.GetFromJsonAsync<JsonElement>(path, Ct);

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static async Task<string?> NicknameErrorOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").GetProperty("nickname")[0]
            .GetString();
}
