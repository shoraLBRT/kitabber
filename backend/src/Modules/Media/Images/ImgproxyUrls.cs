using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Media.Images;

/// <summary>
/// Where imgproxy is and how its links are signed (ADR-0037).
/// </summary>
/// <param name="BaseUrl">
/// The public address of imgproxy, or of the CDN in front of it, with the path prefix imgproxy is told to expect
/// (<c>IMGPROXY_PATH_PREFIX</c>), for example <c>https://example.org/img</c>.
/// </param>
/// <param name="Key">The signing key: the bytes of <c>IMGPROXY_KEY</c>.</param>
/// <param name="Salt">The signing salt: the bytes of <c>IMGPROXY_SALT</c>.</param>
public sealed record ImgproxyOptions(Uri BaseUrl, byte[] Key, byte[] Salt);

/// <summary>
/// Builds signed imgproxy links (ADR-0037). imgproxy reads the original from the bucket itself
/// (<c>IMGPROXY_BASE_URL</c> is the bucket), so a link names only the storage key, the size and the format; the
/// signature is HMAC-SHA256 with the key over the salt and the path, and imgproxy refuses any link without it, so
/// nobody else can ask for another size, another file or another source.
/// </summary>
public sealed class ImgproxyUrls(ImgproxyOptions options)
{
    /// <summary>The link to the original <paramref name="storageKey"/> in <paramref name="variant"/>.</summary>
    public Uri Build(string storageKey, ImageVariant variant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentNullException.ThrowIfNull(variant);
        var path = Path(storageKey, variant);
        return new Uri($"{options.BaseUrl.AbsoluteUri.TrimEnd('/')}/{Sign(path)}{path}");
    }

    /// <summary>The signature of <paramref name="path"/>, as imgproxy computes it.</summary>
    public string Sign(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var data = new byte[options.Salt.Length + Encoding.UTF8.GetByteCount(path)];
        options.Salt.CopyTo(data, 0);
        Encoding.UTF8.GetBytes(path, data.AsSpan(options.Salt.Length));
        return Base64Url(HMACSHA256.HashData(options.Key, data));
    }

    /// <summary>
    /// The path after the signature: resizing (cut to the box around the centre, or fit to the width; never enlarged),
    /// the source as URL-safe Base64, and the format as the extension.
    /// </summary>
    public static string Path(string storageKey, ImageVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        CheckSide(variant.Width, nameof(variant.Width));
        if (variant.Height is { } height)
        {
            CheckSide(height, nameof(variant.Height));
        }

        var resize = variant.Height is { } h
            ? string.Create(CultureInfo.InvariantCulture, $"rs:fill:{variant.Width}:{h}:0/g:ce")
            : string.Create(CultureInfo.InvariantCulture, $"rs:fit:{variant.Width}:0:0");
        var extension = variant.Format switch
        {
            ImageFormat.WebP => "webp",
            ImageFormat.Avif => "avif",
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant.Format, "Unknown image format."),
        };
        return $"/{resize}/{Base64Url(Encoding.UTF8.GetBytes(storageKey))}.{extension}";
    }

    private static void CheckSide(int value, string name)
    {
        if (value is < 1 or > ImageVariant.MaxSide)
        {
            throw new ArgumentOutOfRangeException(name, value, $"A side is from 1 to {ImageVariant.MaxSide} pixels.");
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
