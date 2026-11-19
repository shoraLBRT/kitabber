using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Messaging.IntegrationTests.SampleModule;
using Platform.Testing.Containers;
using static Platform.Messaging.IntegrationTests.TestHosts;

namespace Platform.Messaging.IntegrationTests;

public sealed record WorkerPing(Guid Id);

public sealed class WorkerPingConsumer(ReceivedLog log) : IConsumer<WorkerPing>
{
    public Task Consume(ConsumeContext<WorkerPing> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        log.Add(context.Message.Id);
        return Task.CompletedTask;
    }
}

/// <summary>The workers host on RabbitMQ: queues are durable, so a restart loses nothing.</summary>
public sealed class WorkerRestartTests(RabbitMqFixture rabbitMq)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task MessagesPublishedWhileTheWorkerIsDownAreConsumedAfterRestart()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = RabbitMq(rabbitMq.ConnectionString);
        var log = new ReceivedLog();
        var queue = $"worker-ping-{Guid.NewGuid():N}";

        IHost StartWorker() => Worker(
            settings,
            services => services.AddSingleton(log),
            bus => bus.AddConsumer<WorkerPingConsumer>().Endpoint(endpoint => endpoint.Name = queue));

        using var publisher = Worker(settings);
        await publisher.StartAsync(ct);
        var bus = publisher.Services.GetRequiredService<IBus>();

        var first = Guid.NewGuid();
        using (var worker = StartWorker())
        {
            await worker.StartAsync(ct);
            await bus.Publish(new WorkerPing(first), ct);
            await Eventually(() => log.Count(first) == 1, _timeout, "the running worker consumes the first message");
            await worker.StopAsync(ct);
        }

        var whileDown = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in whileDown)
        {
            await bus.Publish(new WorkerPing(id), ct);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        Assert.Equal(1, log.Total);

        using (var worker = StartWorker())
        {
            await worker.StartAsync(ct);
            await Eventually(() => whileDown.All(id => log.Count(id) == 1), _timeout, "the restarted worker consumes the backlog");
            await worker.StopAsync(ct);
        }

        Assert.Equal(1 + whileDown.Count, log.Total);
        await publisher.StopAsync(ct);
    }
}
