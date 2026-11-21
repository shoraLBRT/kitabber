using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Platform.Modules.Media.IntegrationTests;
using Platform.Testing.Containers;

// One PostgreSQL, one Redis and one MinIO container for the whole assembly; each API test class creates its own
// database (ADR-0008).
[assembly: AssemblyFixture(typeof(PostgresFixture))]
[assembly: AssemblyFixture(typeof(RedisFixture))]
[assembly: AssemblyFixture(typeof(MinioFixture))]

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>MinIO from the image of compose.yaml: the S3 storage of media on a developer's machine (ADR-0030).</summary>
public sealed class MinioFixture : ContainerFixture<IContainer>
{
    public const string AccessKey = "platform";
    public const string SecretKey = "platform-test";
    private const ushort _port = 9000;

    protected override string ImageName => "cgr.dev/chainguard/minio";

    /// <summary>The S3 endpoint of the container.</summary>
    public Uri ServiceUrl => new($"http://{Container.Hostname}:{Container.GetMappedPublicPort(_port)}/");

    protected override IContainer Build(string image) => new ContainerBuilder(image)
        .WithCommand("server", "/data")
        .WithEnvironment("MINIO_ROOT_USER", AccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
        .WithPortBinding(_port, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(_port).ForPath("/minio/health/ready")))
        .Build();
}
