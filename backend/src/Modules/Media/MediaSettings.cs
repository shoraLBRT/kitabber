using System.Globalization;
using Microsoft.Extensions.Configuration;
using Platform.Configuration;
using Platform.Modules.Media.Images;
using Platform.Modules.Media.Storage;
using Platform.Modules.Media.Uploads;

namespace Platform.Modules.Media;

/// <summary>The object storage of media: an S3-compatible service, or the in-memory fake for tests.</summary>
public enum StorageProvider
{
    S3,
    InMemory,
}

/// <summary>The settings of the Media module (ADR-0030); each one is described in docs/CONFIG.md.</summary>
public static class MediaSettings
{
    public const string ConnectionStringName = "Platform";
    public const string DatabaseKey = "ConnectionStrings:" + ConnectionStringName;
    public const string ProviderKey = "Media:Storage:Provider";
    public const string ServiceUrlKey = "Media:Storage:ServiceUrl";
    public const string RegionKey = "Media:Storage:Region";
    public const string BucketKey = "Media:Storage:Bucket";
    public const string AccessKeyKey = "Media:Storage:AccessKey";
    public const string SecretKeyKey = "Media:Storage:SecretKey";
    public const string CreateBucketKey = "Media:Storage:CreateBucket";
    public const string ImageMaxBytesKey = "Media:MaxBytes:Image";
    public const string VideoMaxBytesKey = "Media:MaxBytes:Video";
    public const string AudioMaxBytesKey = "Media:MaxBytes:Audio";
    public const string FileMaxBytesKey = "Media:MaxBytes:File";
    public const string ImagesBaseUrlKey = "Media:Images:BaseUrl";
    public const string ImagesKeyKey = "Media:Images:Key";
    public const string ImagesSaltKey = "Media:Images:Salt";
    public const string DefaultRegion = "us-east-1";

    /// <summary>
    /// The settings the API host checks for this module. The platform database (<see cref="DatabaseKey"/>) is not
    /// repeated here: the host already requires it.
    /// </summary>
    public static IReadOnlyList<Setting> All { get; } =
    [
        new(ProviderKey, "object storage of media: S3, or InMemory for tests")
        {
            Default = nameof(StorageProvider.S3),
            AllowedValues = Enum.GetNames<StorageProvider>(),
        },
        new(ServiceUrlKey, "S3 endpoint, for example http://localhost:9000 for MinIO") { RequiredWhen = IsS3 },
        new(RegionKey, "S3 signing region") { Default = DefaultRegion },
        new(BucketKey, "S3 bucket of media") { RequiredWhen = IsS3 },
        new(AccessKeyKey, "S3 access key id") { RequiredWhen = IsS3 },
        new(SecretKeyKey, "S3 secret access key") { Secret = true, RequiredWhen = IsS3 },
        new(CreateBucketKey, "create the bucket when it is missing: true for MinIO on a developer's machine")
        {
            Default = "false",
            AllowedValues = ["true", "false"],
        },
        new(ImageMaxBytesKey, "largest image upload in bytes") { Default = Bytes(MediaLimits.Default.Image) },
        new(VideoMaxBytesKey, "largest video upload in bytes") { Default = Bytes(MediaLimits.Default.Video) },
        new(AudioMaxBytesKey, "largest audio upload in bytes") { Default = Bytes(MediaLimits.Default.Audio) },
        new(FileMaxBytesKey, "largest upload of another file (PDF) in bytes") { Default = Bytes(MediaLimits.Default.File) },
        new(ImagesBaseUrlKey, "public address of imgproxy or of the CDN in front of it, for example http://localhost:8081")
        {
            RequiredWhen = Setting.Always,
        },
        new(ImagesKeyKey, "imgproxy signing key in hex: the value of IMGPROXY_KEY") { Secret = true, RequiredWhen = Setting.Always },
        new(ImagesSaltKey, "imgproxy signing salt in hex: the value of IMGPROXY_SALT") { Secret = true, RequiredWhen = Setting.Always },
    ];

    public static StorageProvider ReadProvider(IConfiguration configuration) =>
        Enum.TryParse<StorageProvider>(configuration[ProviderKey], ignoreCase: true, out var provider)
            ? provider
            : StorageProvider.S3;

    /// <summary>The size limits; a value that is not a positive whole number of bytes stops the host.</summary>
    public static MediaLimits ReadLimits(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = MediaLimits.Default;
        return new MediaLimits(
            ReadBytes(configuration, ImageMaxBytesKey, defaults.Image),
            ReadBytes(configuration, VideoMaxBytesKey, defaults.Video),
            ReadBytes(configuration, AudioMaxBytesKey, defaults.Audio),
            ReadBytes(configuration, FileMaxBytesKey, defaults.File));
    }

    /// <summary>The S3 bucket and its address; the host has already checked the required ones at start (ADR-0007).</summary>
    public static S3StorageOptions ReadS3(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var url = configuration[ServiceUrlKey];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var serviceUrl))
        {
            throw new InvalidOperationException($"{ServiceUrlKey}: '{url}' is not an absolute URL.");
        }

        return new S3StorageOptions(
            serviceUrl,
            configuration[RegionKey] is { Length: > 0 } region ? region : DefaultRegion,
            configuration[BucketKey]!,
            configuration[AccessKeyKey]!,
            configuration[SecretKeyKey]!,
            string.Equals(configuration[CreateBucketKey], "true", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The address and the signing key and salt of imgproxy (ADR-0037); an address that is not absolute, or a key or
    /// salt that is not hex, stops the host.
    /// </summary>
    public static ImgproxyOptions ReadImages(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var url = configuration[ImagesBaseUrlKey];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUrl) || (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{ImagesBaseUrlKey}: '{url}' is not an absolute http(s) URL.");
        }

        return new ImgproxyOptions(baseUrl, ReadHex(configuration, ImagesKeyKey), ReadHex(configuration, ImagesSaltKey));
    }

    private static byte[] ReadHex(IConfiguration configuration, string key)
    {
        try
        {
            var bytes = Convert.FromHexString(configuration[key] ?? string.Empty);
            return bytes.Length > 0 ? bytes : throw new FormatException("empty");
        }
        catch (FormatException)
        {
            // The value is a secret: it is not repeated in the message.
            throw new InvalidOperationException($"{key}: the value is not a non-empty hex string.");
        }
    }

    private static bool IsS3(IConfiguration configuration) => ReadProvider(configuration) == StorageProvider.S3;

    private static string Bytes(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static long ReadBytes(IConfiguration configuration, string key, long fallback)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) && bytes > 0
            ? bytes
            : throw new InvalidOperationException($"{key}: '{value}' is not a positive number of bytes.");
    }
}
