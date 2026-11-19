using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Messaging.IntegrationTests.SampleModule;
using static Platform.Messaging.IntegrationTests.TestHosts;

namespace Platform.Messaging.IntegrationTests;

/// <summary>The in-memory transport: the bus fake for tests that do not need a broker.</summary>
public sealed class InMemoryTransportTests
{
    [Fact]
    public async Task WorkerOnTheInMemoryTransportConsumesPublishedMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new ReceivedLog();
        using var host = Worker(
            new Dictionary<string, string?> { ["Messaging:Transport"] = "InMemory" },
            services => services.AddSingleton(log),
            bus => bus.AddConsumer<WorkerPingConsumer>());
        await host.StartAsync(ct);

        var id = Guid.NewGuid();
        await host.Services.GetRequiredService<IBus>().Publish(new WorkerPing(id), ct);

        await Eventually(() => log.Count(id) == 1, TimeSpan.FromSeconds(10), "the consumer receives the message");
        await host.StopAsync(ct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("RabbitMq")]
    public void RabbitMqTransportWithoutAConnectionStringIsAClearError(string? transport)
    {
        var configuration = Configuration(new() { ["Messaging:Transport"] = transport });

        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddPlatformMessaging(configuration));
        Assert.Contains("ConnectionStrings:RabbitMq", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownTransportIsRejected()
    {
        var configuration = Configuration(new() { ["Messaging:Transport"] = "Kafka" });

        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddPlatformMessaging(configuration));
        Assert.Contains("Kafka", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("amqp://platform:platform-dev@localhost:5672/", "localhost", 5672, "/", "platform", "platform-dev")]
    [InlineData("amqp://user:p%40ss@broker/stand", "broker", 5672, "stand", "user", "p@ss")]
    [InlineData("amqp://rabbit:15672", "rabbit", 15672, "/", "guest", "guest")]
    public void RabbitMqConnectionStringIsParsed(
        string connectionString, string host, int port, string vhost, string user, string password)
    {
        var endpoint = RabbitMqEndpoint.Parse(connectionString);

        Assert.Equal(new RabbitMqEndpoint(host, (ushort)port, vhost, user, password), endpoint);
        Assert.DoesNotContain($":{password}@", endpoint.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("localhost:5672")]
    [InlineData("http://localhost:5672/")]
    public void RabbitMqConnectionStringMustBeAnAmqpUri(string connectionString) =>
        Assert.Throws<InvalidOperationException>(() => RabbitMqEndpoint.Parse(connectionString));

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
