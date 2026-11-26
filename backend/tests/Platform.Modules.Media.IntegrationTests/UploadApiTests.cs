using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Media.Storage;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>
/// The Api host with the Media module, on PostgreSQL and Redis in Testcontainers, the in-memory object storage and the
/// test sign-in. Images are limited to <see cref="ImageLimit"/> bytes to show the limit is a setting.
/// </summary>
public sealed class MediaApi : IntegrationApiFactory
{
    public const int ImageLimit = 4096;

    public MediaApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
        WithModule(services => services.AddTestAuthentication());
        WithSetting(MediaSettings.ImageMaxBytesKey, ImageLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public InMemoryObjectStorage Storage => Services.GetRequiredService<InMemoryObjectStorage>();
}

/// <summary>E06-01: <c>POST /api/v1/media</c> — the content decides, the limits are settings, the original is bound to its owner.</summary>
public sealed class UploadApiTests(MediaApi api) : IClassFixture<MediaApi>, IAsyncLifetime
{
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        api.Storage.Clear();
        _client = api.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task OwnerUploadsAnImageAndItsOriginalIsStoredUnderARandomKey()
    {
        var owner = NewAccount();
        var bytes = SampleFiles.Png(1000);

        using var response = await UploadAsync(owner, bytes, "cat.png", declaredType: "application/octet-stream");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("image", body.GetProperty("kind").GetString());
        // The type the content showed, not the one the client declared.
        Assert.Equal("image/png", body.GetProperty("contentType").GetString());
        Assert.Equal(1000, body.GetProperty("size").GetInt64());
        Assert.Equal("cat.png", body.GetProperty("fileName").GetString());
        Assert.Equal(api.Time.GetUtcNow(), body.GetProperty("createdAt").GetDateTimeOffset());
        Assert.False(body.TryGetProperty("storageKey", out _));

        var file = await FindAsync(body.GetProperty("id").GetGuid());
        Assert.Equal(owner, file.OwnerAccountId);
        Assert.Matches("^originals/[0-9a-f]{32}$", file.StorageKey);
        Assert.DoesNotContain(owner.ToString("N"), file.StorageKey, StringComparison.Ordinal);
        Assert.DoesNotContain(file.Id.ToString("N"), file.StorageKey, StringComparison.Ordinal);
        Assert.DoesNotContain("cat", file.StorageKey, StringComparison.Ordinal);
        var stored = await api.Storage.FindAsync(file.StorageKey, Ct);
        Assert.Equal(new StoredObject(file.StorageKey, "image/png", 1000), stored);
        await using var original = (await api.Storage.OpenReadAsync(file.StorageKey, Ct))!;
        using var copy = new MemoryStream();
        await original.CopyToAsync(copy, Ct);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task ImageGetsItsPreviewAndColourStoredWithTheFile()
    {
        // Under the test limit of MediaApi.ImageLimit bytes.
        using var response = await UploadAsync(NewAccount(), Pictures.Png(32, 24), "gradient.png");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var preview = body.GetProperty("preview");
        Assert.Matches("^#[0-9a-f]{6}$", preview.GetProperty("color").GetString());
        var image = preview.GetProperty("image").GetString()!;
        Assert.StartsWith("data:image/webp;base64,", image, StringComparison.Ordinal);

        // Kept with the file itself: the same colour and picture come back later without decoding the original again.
        var file = await FindAsync(body.GetProperty("id").GetGuid());
        Assert.Equal(preview.GetProperty("color").GetString(), file.PreviewColor);
        Assert.Equal(image["data:image/webp;base64,".Length..], Convert.ToBase64String(file.PreviewImage!));
        Assert.Equal((16, 12), Pictures.SizeOf(file.PreviewImage!));
    }

    [Theory]
    [InlineData("broken.png", nameof(SampleFiles.Png))]
    [InlineData("report.pdf", nameof(SampleFiles.Pdf))]
    public async Task UndecodableImageAndOtherFilesAreAcceptedWithoutAPreview(string name, string sample)
    {
        var bytes = (byte[])typeof(SampleFiles).GetMethod(sample)!.Invoke(null, [512])!;

        using var response = await UploadAsync(NewAccount(), bytes, name);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("preview").ValueKind);
        var file = await FindAsync(body.GetProperty("id").GetGuid());
        Assert.Null(file.PreviewColor);
        Assert.Null(file.PreviewImage);
    }

    [Fact]
    public async Task SameFileUploadedTwiceGetsTwoUnrelatedKeys()
    {
        var owner = NewAccount();

        using var first = await UploadAsync(owner, SampleFiles.Jpeg(), "a.jpg");
        using var second = await UploadAsync(owner, SampleFiles.Jpeg(), "a.jpg");

        var a = await FindAsync(await IdOf(first));
        var b = await FindAsync(await IdOf(second));
        Assert.NotEqual(a.StorageKey, b.StorageKey);
        Assert.Equal(2, api.Storage.Keys.Count);
    }

    [Theory]
    [InlineData("virus.jpg", nameof(SampleFiles.Executable), "media_type_unsupported")]
    [InlineData("page.png", nameof(SampleFiles.Html), "media_type_unsupported")]
    [InlineData("photo.jpg", nameof(SampleFiles.Png), "media_extension_mismatch")]
    [InlineData("clip.mp4", nameof(SampleFiles.Pdf), "media_extension_mismatch")]
    public async Task FileWithASwappedExtensionIsRefusedAndNothingIsStored(string name, string sample, string code)
    {
        var bytes = (byte[])typeof(SampleFiles).GetMethod(sample)!.Invoke(null, [512])!;

        using var response = await UploadAsync(NewAccount(), bytes, name, declaredType: "image/jpeg");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(code, await CodeOf(response));
        await AssertNothingStoredAsync();
    }

    [Fact]
    public async Task ImageOverTheConfiguredLimitIsRefused()
    {
        using var atLimit = await UploadAsync(NewAccount(), SampleFiles.Png(MediaApi.ImageLimit), "a.png");
        using var overLimit = await UploadAsync(NewAccount(), SampleFiles.Png(MediaApi.ImageLimit + 1), "b.png");

        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, overLimit.StatusCode);
        Assert.Equal("media_too_large", await CodeOf(overLimit));
        Assert.Single(api.Storage.Keys);
    }

    [Fact]
    public async Task OtherKindsKeepTheirOwnLimit()
    {
        // Larger than the image limit, far below the default limit of files.
        using var response = await UploadAsync(NewAccount(), SampleFiles.Pdf(MediaApi.ImageLimit * 4), "report.pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("file", body.GetProperty("kind").GetString());
        Assert.Equal("application/pdf", body.GetProperty("contentType").GetString());
    }

    [Fact]
    public async Task UploadNeedsSignIn()
    {
        using var response = await UploadAsync(account: null, SampleFiles.Png(), "a.png");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNothingStoredAsync();
    }

    [Fact]
    public async Task EmptyFileIsAValidationError()
    {
        using var response = await UploadAsync(NewAccount(), [], "a.png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await CodeOf(response));
        await AssertNothingStoredAsync();
    }

    [Fact]
    public async Task RequestWithoutAFileIsAValidationError()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media")
        {
            Content = new MultipartFormDataContent { { new StringContent("x"), "note" } },
        };
        request.As(NewAccount().ToString("D"));

        using var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await CodeOf(response));
    }

    private Guid NewAccount() => Guid.CreateVersion7(api.Time.GetUtcNow());

    private async Task<HttpResponseMessage> UploadAsync(
        Guid? account, byte[] bytes, string fileName, string declaredType = "application/octet-stream")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(declaredType);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media")
        {
            Content = new MultipartFormDataContent { { file, "file", fileName } },
        };
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<MediaFile> FindAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        return await db.Files.AsNoTracking().SingleAsync(f => f.Id == id, Ct);
    }

    private async Task AssertNothingStoredAsync()
    {
        Assert.Empty(api.Storage.Keys);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.Equal(0, await db.Files.CountAsync(Ct));
    }

    private static async Task<Guid> IdOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
