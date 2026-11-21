using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Platform.Modules.Media.Images;
using Platform.Modules.Media.Storage;
using Platform.Testing.Containers;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>
/// imgproxy from the image of compose.yaml, reading the originals from its own MinIO over a private network, set up
/// as compose.yaml and the stand set it up (ADR-0037): the bucket as the base of every source, signed links only,
/// ETag for conditional requests.
/// </summary>
public sealed class ImgproxyFixture : ContainerFixture<IContainer>
{
    public const string Bucket = "media";
    private const ushort _port = 8080;
    private const ushort _minioPort = 9000;

    // Throw-away values for this container only, in hex as imgproxy reads them.
    private static readonly byte[] _key = Convert.FromHexString("0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f9");
    private static readonly byte[] _salt = Convert.FromHexString("f9e8d7c6b5a4938271605f4e3d2c1b0af9e8d7c6b5a4938271605f4e3d2c1b0a");

    private INetwork? _network;
    private IContainer? _minio;

    protected override string ImageName => "darthsim/imgproxy";

    /// <summary>The address of imgproxy, as <c>Media:Images:BaseUrl</c> would give it.</summary>
    public Uri BaseUrl => new($"http://{Container.Hostname}:{Container.GetMappedPublicPort(_port)}/");

    /// <summary>Links signed with the key and salt imgproxy was started with.</summary>
    public ImgproxyUrls Urls => new(new ImgproxyOptions(BaseUrl, _key, _salt));

    /// <summary>The bucket imgproxy reads, created on first use.</summary>
    public S3ObjectStorage CreateStorage()
    {
        _ = Container;
        return new S3ObjectStorage(new S3StorageOptions(
            new Uri($"http://{_minio!.Hostname}:{_minio.GetMappedPublicPort(_minioPort)}/"),
            MediaSettings.DefaultRegion,
            Bucket,
            MinioFixture.AccessKey,
            MinioFixture.SecretKey,
            CreateBucket: true));
    }

    protected override IContainer Build(string image)
    {
        _network = new NetworkBuilder().Build();
        _minio = new ContainerBuilder(ComposeImage("cgr.dev/chainguard/minio"))
            .WithNetwork(_network)
            .WithNetworkAliases("minio")
            .WithCommand("server", "/data")
            .WithEnvironment("MINIO_ROOT_USER", MinioFixture.AccessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", MinioFixture.SecretKey)
            .WithPortBinding(_minioPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(_minioPort).ForPath("/minio/health/ready")))
            .Build();
        return new ContainerBuilder(image)
            .DependsOn(_minio)
            .WithNetwork(_network)
            .WithEnvironment("IMGPROXY_KEY", Convert.ToHexStringLower(_key))
            .WithEnvironment("IMGPROXY_SALT", Convert.ToHexStringLower(_salt))
            .WithEnvironment("IMGPROXY_USE_S3", "true")
            .WithEnvironment("IMGPROXY_S3_ENDPOINT", $"http://minio:{_minioPort}")
            .WithEnvironment("IMGPROXY_S3_REGION", MediaSettings.DefaultRegion)
            .WithEnvironment("AWS_ACCESS_KEY_ID", MinioFixture.AccessKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", MinioFixture.SecretKey)
            .WithEnvironment("IMGPROXY_BASE_URL", $"s3://{Bucket}/")
            .WithEnvironment("IMGPROXY_USE_ETAG", "true")
            .WithPortBinding(_port, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(_port).ForPath("/health")))
            .Build();
    }
}
