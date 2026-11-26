using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E04-04: the owner sets the cover of the personal blog and its position through <c>PUT /api/v1/profiles/me/cover</c>
/// and removes it through <c>DELETE /api/v1/profiles/me/cover</c>; everyone sees the same picture at the same position
/// (ADR-0079).
/// </summary>
public sealed class ProfileCoverApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
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
    public async Task OwnerSetsTheCoverAndEveryoneSeesItAtTheSamePosition()
    {
        var blog = await SaveDemoBlogAsync();
        var account = blog.OwnerAccountId!.Value;
        var cover = await UploadAsync(account, Png(), "cover.png");

        using var response = await SendAsync(HttpMethod.Put, account, new { mediaId = cover, position = 30 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("cover");
        Assert.Equal(cover, saved.GetProperty("id").GetGuid());
        Assert.Equal(30, saved.GetProperty("position").GetInt32());
        // A guest reads the same cover: the position belongs to the blog, not to a screen.
        var seen = (await GetAsync($"/api/v1/profiles/{blog.Id}")).GetProperty("cover");
        Assert.Equal(cover, seen.GetProperty("id").GetGuid());
        Assert.Equal(30, seen.GetProperty("position").GetInt32());
        var sources = seen.GetProperty("sources").EnumerateArray().ToList();
        Assert.Equal([400, 640, 800, 1280, 1920], sources.Select(s => s.GetProperty("width").GetInt32()));
        Assert.All(sources, s => Assert.StartsWith(
            PlatformApiFactory.ImgproxyBaseUrl, s.GetProperty("url").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task OwnerMovesTheCoverWithoutUploadingItAgain()
    {
        var blog = await SaveDemoBlogAsync();
        var account = blog.OwnerAccountId!.Value;
        var cover = await UploadAsync(account, Png(), "cover.png");
        (await SendAsync(HttpMethod.Put, account, new { mediaId = cover, position = 10 })).Dispose();

        using var moved = await SendAsync(HttpMethod.Put, account, new { mediaId = cover, position = 100 });

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var seen = (await GetAsync($"/api/v1/profiles/by-nickname/{blog.Nickname}")).GetProperty("cover");
        Assert.Equal(100, seen.GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task OwnerRemovesTheCover()
    {
        var blog = await SaveDemoBlogAsync();
        var account = blog.OwnerAccountId!.Value;
        var cover = await UploadAsync(account, Png(), "cover.png");
        (await SendAsync(HttpMethod.Put, account, new { mediaId = cover, position = 70 })).Dispose();

        using var response = await SendAsync(HttpMethod.Delete, account);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("cover").ValueKind);
        var seen = await GetAsync($"/api/v1/profiles/{blog.Id}");
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("cover").ValueKind);
    }

    [Fact]
    public async Task ProfileWithoutACoverHasNone()
    {
        var blog = await SaveDemoBlogAsync();

        var seen = await GetAsync($"/api/v1/profiles/{blog.Id}");

        Assert.Equal(JsonValueKind.Null, seen.GetProperty("cover").ValueKind);
    }

    [Fact]
    public async Task CoverMustBeTheOwnersOwnImageAtAPositionInRange()
    {
        var blog = await SaveDemoBlogAsync();
        var account = blog.OwnerAccountId!.Value;
        var stranger = Guid.CreateVersion7(api.Time.GetUtcNow());
        var strangersImage = await UploadAsync(stranger, Png(), "theirs.png");
        var ownPdf = await UploadAsync(account, [.. "%PDF-1.7\n"u8, .. new byte[200]], "cv.pdf");
        var ownImage = await UploadAsync(account, Png(), "cover.png");

        foreach (var mediaId in new[] { strangersImage, ownPdf, Guid.CreateVersion7(api.Time.GetUtcNow()) })
        {
            using var response = await SendAsync(HttpMethod.Put, account, new { mediaId, position = 50 });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
            Assert.True(problem.GetProperty("errors").TryGetProperty("mediaId", out _));
        }

        foreach (var position in new[] { -1, 101 })
        {
            using var response = await SendAsync(HttpMethod.Put, account, new { mediaId = ownImage, position });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.True(problem.GetProperty("errors").TryGetProperty("position", out _));
        }

        var seen = await GetAsync($"/api/v1/profiles/{blog.Id}");
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("cover").ValueKind);
    }

    [Fact]
    public async Task ChangingTheCoverNeedsSignIn()
    {
        using var put = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profiles/me/cover")
        {
            Content = JsonContent.Create(new { mediaId = Guid.CreateVersion7(api.Time.GetUtcNow()), position = 50 }),
        };
        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/profiles/me/cover");

        using var putResponse = await _client.SendAsync(put, Ct);
        using var deleteResponse = await _client.SendAsync(delete, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
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

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Guid account, object? body = null)
    {
        using var request = new HttpRequestMessage(method, "/api/v1/profiles/me/cover");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.As(account.ToString("D"));
        return await _client.SendAsync(request, Ct);
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        using var response = await _client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
