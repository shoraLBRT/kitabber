using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Platform.Testing.Containers;

public sealed class RabbitMqFixture : ContainerFixture<RabbitMqContainer>
{
    protected override string ImageName => "rabbitmq";

    /// <summary><c>amqp://user:password@host:port/</c> of the broker.</summary>
    public string ConnectionString => Container.GetConnectionString();

    protected override RabbitMqContainer Build(string image) => new RabbitMqBuilder(image).Build();
}

public sealed class RedisFixture : ContainerFixture<RedisContainer>
{
    /// <summary>Redis has 16 numbered databases by default; database 0 stays for <see cref="ConnectionString"/>.</summary>
    private const int _databaseCount = 16;

    private int _lastDatabase;

    protected override string ImageName => "redis";

    /// <summary>The server, database 0: shared by every test that uses it.</summary>
    public string ConnectionString => Container.GetConnectionString();

    /// <summary>
    /// A numbered database of its own (1..15), so a test class can empty it with <see cref="FlushAsync"/> without
    /// touching the others. One per test class, not per test: there are only 15.
    /// </summary>
    public string CreateDatabase()
    {
        var server = ConnectionString;
        var number = Interlocked.Increment(ref _lastDatabase);
        if (number >= _databaseCount)
        {
            throw new InvalidOperationException(
                $"All {_databaseCount - 1} Redis test databases are taken: share one per test class, not per test.");
        }

        return $"{server},defaultDatabase={number},allowAdmin=true";
    }

    /// <summary>Removes every key of the database a <see cref="CreateDatabase"/> connection string points to.</summary>
    public static async Task FlushAsync(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        await using var redis = await ConnectionMultiplexer.ConnectAsync(options);
        var server = redis.GetServer(redis.GetEndPoints()[0]);
        await server.FlushDatabaseAsync(options.DefaultDatabase ?? 0);
    }

    protected override RedisContainer Build(string image) => new RedisBuilder(image).Build();
}

public sealed class PostgresFixture : ContainerFixture<PostgreSqlContainer>
{
    protected override string ImageName => "postgres";

    /// <summary>Creates a new empty database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var server = Container.GetConnectionString();
        var name = $"test_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(server))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(server) { Database = name }.ConnectionString;
    }

    protected override PostgreSqlContainer Build(string image) => new PostgreSqlBuilder(image).Build();
}

/// <summary>
/// OpenSearch, the search engine of compose.yaml (ADR-0094), single node without the security plugin, as locally.
/// Tests share it: each gives its indexes a prefix of its own.
/// </summary>
public sealed class OpenSearchFixture : ContainerFixture<IContainer>
{
    private const ushort _port = 9200;

    protected override string ImageName => "opensearchproject/opensearch";

    /// <summary>The REST API of the running container.</summary>
    public Uri Url => new($"http://{Container.Hostname}:{Container.GetMappedPublicPort(_port)}/");

    protected override IContainer Build(string image) => new ContainerBuilder(image)
        .WithPortBinding(_port, assignRandomHostPort: true)
        .WithEnvironment("discovery.type", "single-node")
        .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
        .WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
        .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r
            .ForPort(_port).ForPath("/_cluster/health")))
        .Build();
}

/// <summary>
/// Mailpit, the mail catcher of compose.yaml: the application sends to its SMTP port, a test reads the letter through
/// its API (<see cref="MailpitClient"/>). Accepts any SMTP login, so the authenticated path is tested too.
/// </summary>
public sealed class MailpitFixture : ContainerFixture<IContainer>
{
    private const ushort _smtpPort = 1025;
    private const ushort _apiPort = 8025;

    protected override string ImageName => "axllent/mailpit";

    public string SmtpHost => Container.Hostname;

    public int SmtpMappedPort => Container.GetMappedPublicPort(_smtpPort);

    /// <summary>A client of the Mailpit API; dispose it after the test.</summary>
    public MailpitClient CreateClient() =>
        new(new Uri($"http://{Container.Hostname}:{Container.GetMappedPublicPort(_apiPort)}/"));

    protected override IContainer Build(string image) => new ContainerBuilder(image)
        .WithPortBinding(_smtpPort, assignRandomHostPort: true)
        .WithPortBinding(_apiPort, assignRandomHostPort: true)
        .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "true")
        .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "true")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(_apiPort).ForPath("/readyz")))
        .Build();
}
