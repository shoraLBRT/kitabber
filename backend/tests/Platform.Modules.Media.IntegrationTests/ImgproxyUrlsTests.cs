using System.Text;
using Microsoft.Extensions.Configuration;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Media.Images;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>E06-02: the rules of signed imgproxy links (ADR-0037), without a container.</summary>
public sealed class ImgproxyUrlsTests
{
    private const string _storageKey = "originals/0123456789abcdef0123456789abcdef";

    [Fact]
    public void SignatureIsTheOneOfImgproxyDocumentation()
    {
        // The example of imgproxy's "Signing a URL": key "secret", salt "hello".
        var urls = new ImgproxyUrls(new ImgproxyOptions(
            new Uri("http://img.test/"), Encoding.ASCII.GetBytes("secret"), Encoding.ASCII.GetBytes("hello")));

        Assert.Equal(
            "oKfUtW34Dvo2BGQehJFR4Nr0_rIjOtdtzJ3QFsUcXH8",
            urls.Sign("/rs:fill:300:400:0/g:sm/aHR0cDovL2V4YW1w/bGUuY29tL2ltYWdl/cy9jdXJpb3NpdHku/anBn.png"));
    }

    [Fact]
    public void LinkNamesTheSizeTheFormatAndTheSourceAfterTheSignature()
    {
        var urls = Urls("https://example.org/img");
        var source = Convert.ToBase64String(Encoding.UTF8.GetBytes(_storageKey)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var square = urls.Build(_storageKey, ImageVariant.Square(128, ImageFormat.WebP));
        var wide = urls.Build(_storageKey, ImageVariant.ToWidth(640, ImageFormat.Avif));

        var squarePath = $"/rs:fill:128:128:0/g:ce/{source}.webp";
        Assert.Equal($"https://example.org/img/{urls.Sign(squarePath)}{squarePath}", square.AbsoluteUri);
        Assert.EndsWith($"/rs:fit:640:0:0/{source}.avif", wide.AbsoluteUri, StringComparison.Ordinal);
        Assert.NotEqual(square.Segments[2], wide.Segments[2]);
        Assert.DoesNotContain("+", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherKeyOrSaltSignsDifferently()
    {
        var path = ImgproxyUrls.Path(_storageKey, ImageVariant.Square(64, ImageFormat.WebP));

        var signature = Urls("http://img.test").Sign(path);

        Assert.NotEqual(signature, new ImgproxyUrls(new ImgproxyOptions(new Uri("http://img.test/"), [1, 2, 3], [4, 5, 7])).Sign(path));
        Assert.NotEqual(signature, new ImgproxyUrls(new ImgproxyOptions(new Uri("http://img.test/"), [1, 2, 4], [4, 5, 6])).Sign(path));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(ImageVariant.MaxSide + 1, null)]
    [InlineData(100, 0)]
    [InlineData(100, ImageVariant.MaxSide + 1)]
    public void SideOutsideTheLimitsIsRefused(int width, int? height) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Urls("http://img.test").Build(_storageKey, new ImageVariant(width, height, ImageFormat.WebP)));

    [Fact]
    public void SettingsGiveTheAddressAndTheKeys()
    {
        var options = MediaSettings.ReadImages(Settings("https://example.org/img", "0a0b", "0c0d"));

        Assert.Equal(new Uri("https://example.org/img"), options.BaseUrl);
        Assert.Equal(new byte[] { 0x0a, 0x0b }, options.Key);
        Assert.Equal(new byte[] { 0x0c, 0x0d }, options.Salt);
    }

    [Theory]
    [InlineData("/img", "0a0b", "0c0d")]
    [InlineData("ftp://example.org", "0a0b", "0c0d")]
    [InlineData("https://example.org", "not hex", "0c0d")]
    [InlineData("https://example.org", "0a0b", "")]
    public void WrongSettingsStopTheHost(string url, string key, string salt)
    {
        var error = Assert.Throws<InvalidOperationException>(() => MediaSettings.ReadImages(Settings(url, key, salt)));

        Assert.DoesNotContain("not hex", error.Message, StringComparison.Ordinal);
    }

    private static ImgproxyUrls Urls(string baseUrl) => new(new ImgproxyOptions(new Uri(baseUrl), [1, 2, 3], [4, 5, 6]));

    private static IConfiguration Settings(string url, string key, string salt) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            [MediaSettings.ImagesBaseUrlKey] = url,
            [MediaSettings.ImagesKeyKey] = key,
            [MediaSettings.ImagesSaltKey] = salt,
        })
        .Build();
}
