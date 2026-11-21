using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Modules.Media.Images;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E04-03: the owner edits the name, the description and the avatar of the personal blog through
/// <c>PUT /api/v1/profiles/me</c>; the avatar is a file uploaded through <c>POST /api/v1/media</c> (ADR-0034).
/// </summary>
public sealed class ProfileEditApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
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
    public async Task OwnerChangesTheProfileAndEveryoneSeesItAtOnce()
    {
        var blog = await SaveDemoBlogAsync();
        var account = blog.OwnerAccountId!.Value;
        var avatar = await UploadAsync(account, Png(), "avatar.png");

        using var response = await PutAsync(account, new { name = "  Анна К.  ", description = "О книгах 📚", avatarId = avatar });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Анна К.", saved.GetProperty("name").GetString());
        Assert.Equal(avatar, saved.GetProperty("avatarId").GetGuid());
        var seen = await GetAsync($"/api/v1/profiles/{blog.Id}");
        Assert.Equal("Анна К.", seen.GetProperty("name").GetString());
        Assert.Equal("О книгах 📚", seen.GetProperty("description").GetString());
        Assert.Equal(avatar, seen.GetProperty("avatarId").GetGuid());
        Assert.Equal("demo0", seen.GetProperty("nickname").GetString());
        AssertAvatarLink(seen.GetProperty("avatarUrl").GetString());
        Assert.Equal(seen.GetProperty("avatarUrl").GetString(), saved.GetProperty("avatarUrl").GetString());
        // One source per screen density, narrowest first, each a signed square of its own width (ADR-0040).
        var sources = seen.GetProperty("avatarSources").EnumerateArray().ToList();
        Assert.Equal([64, 128, 192], sources.Select(s => s.GetProperty("width").GetInt32()));
        foreach (var source in sources)
        {
            AssertAvatarLink(source.GetProperty("url").GetString(), source.GetProperty("width").GetInt32());
        }

        Assert.Equal(seen.GetProperty("avatarUrl").GetString(), sources[1].GetProperty("url").GetString());
    }

    [Fact]
    public async Task ProfileWithoutAnAvatarHasNoAvatarLink()
    {
        var blog = await SaveDemoBlogAsync();

        var seen = await GetAsync($"/api/v1/profiles/{blog.Id}");

        Assert.Equal(JsonValueKind.Null, seen.GetProperty("avatarUrl").ValueKind);
        Assert.Equal(0, seen.GetProperty("avatarSources").GetArrayLength());
    }

    [Fact]
    public async Task EmptyFieldsClearTheProfile()
    {
        var blog = await SaveDemoBlogAsync();
        var account = blog.OwnerAccountId!.Value;
        var avatar = await UploadAsync(account, Png(), "avatar.png");
        (await PutAsync(account, new { name = "Анна", description = "Описание", avatarId = avatar })).Dispose();

        using var response = await PutAsync(account, new { name = " ", description = "" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seen = await GetAsync($"/api/v1/profiles/{blog.Id}");
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("avatarId").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task LimitsAreCheckedInCharactersPeopleSee()
    {
        var blog = await SaveDemoBlogAsync();
        // 25 emoji are 50 UTF-16 units but 25 characters: allowed; 26 are not.
        using var fits = await PutAsync(
            blog.OwnerAccountId!.Value,
            new { name = string.Concat(Enumerable.Repeat("📚", 25)), description = new string('я', 160) });
        using var tooLong = await PutAsync(
            blog.OwnerAccountId!.Value,
            new { name = string.Concat(Enumerable.Repeat("📚", 26)), description = new string('я', 161) });

        Assert.Equal(HttpStatusCode.OK, fits.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var problem = await tooLong.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("description", out _));
        // Nothing of a rejected edit is saved.
        var seen = await GetAsync($"/api/v1/profiles/{blog.Id}");
        Assert.Equal(25, new System.Globalization.StringInfo(seen.GetProperty("name").GetString()!).LengthInTextElements);
    }

    [Fact]
    public async Task AvatarMustBeTheOwnersOwnImage()
    {
        var blog = await SaveDemoBlogAsync();
        var account = blog.OwnerAccountId!.Value;
        var stranger = Guid.CreateVersion7(api.Time.GetUtcNow());
        var strangersImage = await UploadAsync(stranger, Png(), "theirs.png");
        var ownPdf = await UploadAsync(account, [.. "%PDF-1.7\n"u8, .. new byte[200]], "cv.pdf");

        foreach (var avatarId in new[] { strangersImage, ownPdf, Guid.CreateVersion7(api.Time.GetUtcNow()) })
        {
            using var response = await PutAsync(account, new { name = "Анна", avatarId });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.True(problem.GetProperty("errors").TryGetProperty("avatarId", out _));
        }

        var seen = await GetAsync($"/api/v1/profiles/{blog.Id}");
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("avatarId").ValueKind);
        Assert.Equal(blog.Name, seen.GetProperty("name").GetString());
    }

    [Fact]
    public async Task EditingNeedsSignIn()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me")
        {
            Content = JsonContent.Create(new { name = "Анна" }),
        };

        using var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccountWithoutABlogYetGetsOneWithTheProfile()
    {
        var account = Guid.CreateVersion7(api.Time.GetUtcNow());

        using var response = await PutAsync(account, new { name = "Новичок" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var own = await GetAsync("/api/v1/profiles/me", account);
        Assert.Equal("Новичок", own.GetProperty("name").GetString());
    }

    // A WebP square of 128 px from imgproxy of the test host, signed with its key and salt (ADR-0037).
    private static void AssertAvatarLink(string? link, int side = 128)
    {
        Assert.NotNull(link);
        Assert.StartsWith(PlatformApiFactory.ImgproxyBaseUrl, link, StringComparison.Ordinal);
        var rest = link[PlatformApiFactory.ImgproxyBaseUrl.Length..];
        var signature = rest[..rest.IndexOf('/', StringComparison.Ordinal)];
        var path = rest[signature.Length..];
        Assert.StartsWith($"/rs:fill:{side}:{side}:0/g:ce/", path, StringComparison.Ordinal);
        Assert.EndsWith(".webp", path, StringComparison.Ordinal);
        var urls = new ImgproxyUrls(new ImgproxyOptions(
            new Uri(PlatformApiFactory.ImgproxyBaseUrl),
            Convert.FromHexString(PlatformApiFactory.ImgproxyKey),
            Convert.FromHexString(PlatformApiFactory.ImgproxySalt)));
        Assert.Equal(urls.Sign(path), signature);
    }

    private static byte[] Png() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, .. "IHDR"u8, .. new byte[240]];

    private async Task<Subsite> SaveDemoBlogAsync()
    {
        var blog = new PersonalBlogFactory(api.Data).Build();
        await api.SaveAsync<BlogsDbContext>(blog);
        return blog;
    }

    private async Task<Guid> UploadAsync(Guid account, byte[] bytes, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media")
        {
            Content = new MultipartFormDataContent { { file, "file", fileName } },
        };
        request.As(account.ToString("D"));

        using var response = await _client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> PutAsync(Guid account, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me")
        {
            Content = JsonContent.Create(body),
        };
        request.As(account.ToString("D"));
        return await _client.SendAsync(request, Ct);
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
}
