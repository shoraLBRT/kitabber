using System.Net;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Media.Images;
using Platform.Modules.Media.Storage;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>
/// E06-02: imgproxy of compose.yaml in Testcontainers delivers an original from the bucket in the size and the modern
/// format a link signed by <see cref="ImgproxyUrls"/> asks for, refuses any other link, and lets browsers and the CDN
/// keep the answer (ADR-0037).
/// </summary>
public sealed class ImgproxyDeliveryTests(ImgproxyFixture imgproxy) : IClassFixture<ImgproxyFixture>, IAsyncLifetime
{
    private readonly HttpClient _http = new();
    private string _key = string.Empty;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        using var storage = imgproxy.CreateStorage();
        _key = StorageKeys.NewOriginal();
        await storage.PutAsync(_key, new MemoryStream(Pictures.Png(64, 48)), "image/png", Ct);
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SquareIsCutToTheAskedSizeInWebP()
    {
        using var response = await _http.GetAsync(imgproxy.Urls.Build(_key, ImageVariant.Square(32, ImageFormat.WebP)), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((32, 32), Pictures.SizeOf(await response.Content.ReadAsByteArrayAsync(Ct)));
    }

    [Fact]
    public async Task WidthKeepsTheProportionsInAvif()
    {
        using var response = await _http.GetAsync(imgproxy.Urls.Build(_key, ImageVariant.ToWidth(40, ImageFormat.Avif)), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/avif", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((40, 30), Pictures.SizeOf(await response.Content.ReadAsByteArrayAsync(Ct)));
    }

    [Fact]
    public async Task PictureIsNeverEnlarged()
    {
        using var response = await _http.GetAsync(imgproxy.Urls.Build(_key, ImageVariant.ToWidth(1024, ImageFormat.WebP)), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((64, 48), Pictures.SizeOf(await response.Content.ReadAsByteArrayAsync(Ct)));
    }

    [Fact]
    public async Task LinkWithoutAValidSignatureIsRefused()
    {
        var path = ImgproxyUrls.Path(_key, ImageVariant.Square(32, ImageFormat.WebP));
        var otherSize = imgproxy.Urls.Sign(ImgproxyUrls.Path(_key, ImageVariant.Square(33, ImageFormat.WebP)));
        var signed = imgproxy.Urls.Sign(path);
        var tampered = (signed[0] == 'A' ? "B" : "A") + signed[1..];

        foreach (var signature in new[] { "insecure", "_", otherSize, tampered })
        {
            using var response = await _http.GetAsync(new Uri(imgproxy.BaseUrl, signature + path), Ct);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task AnswerIsCachedForAYearAndRevalidatedByETag()
    {
        var url = imgproxy.Urls.Build(_key, ImageVariant.Square(32, ImageFormat.WebP));

        using var first = await _http.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var cache = first.Headers.CacheControl;
        Assert.NotNull(cache);
        Assert.True(cache.Public);
        Assert.Equal(TimeSpan.FromDays(365), cache.MaxAge);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);

        using var again = new HttpRequestMessage(HttpMethod.Get, url);
        again.Headers.IfNoneMatch.Add(etag);
        using var second = await _http.SendAsync(again, Ct);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }
}
