using Npgsql;
using Platform.Messaging.IntegrationTests.Containers;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(RabbitMqFixture))]
[assembly: AssemblyFixture(typeof(RedisFixture))]
[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Platform.Messaging.IntegrationTests.Containers;

public sealed class RabbitMqFixture : ContainerFixture<RabbitMqContainer>
{
    protected override string ImageName => "rabbitmq";

    /// <summary><c>amqp://user:password@host:port/</c> of the broker.</summary>
    public string ConnectionString => Container.GetConnectionString();

    protected override RabbitMqContainer Build(string image) => new RabbitMqBuilder(image).Build();
}

public sealed class RedisFixture : ContainerFixture<RedisContainer>
{
    protected override string ImageName => "redis";

    public string ConnectionString => Container.GetConnectionString();

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
