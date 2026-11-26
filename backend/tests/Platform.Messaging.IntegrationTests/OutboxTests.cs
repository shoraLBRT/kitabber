using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Messaging.IntegrationTests.SampleModule;
using Platform.Persistence;
using Platform.Testing.Containers;
using static Platform.Messaging.IntegrationTests.TestHosts;

namespace Platform.Messaging.IntegrationTests;

/// <summary>The transactional outbox on PostgreSQL and RabbitMQ (ADR-0005).</summary>
[Collection(TestHosts.SampleEvents)]
public sealed class OutboxTests(RabbitMqFixture rabbitMq, PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private readonly ReceivedLog _log = new();
    private IHost? _host;

    private IServiceProvider Services => _host!.Services;

    public async ValueTask InitializeAsync()
    {
        var broker = rabbitMq.ConnectionString;
        var database = await postgres.CreateDatabaseAsync();
        _host = Worker(
            RabbitMq(broker),
            services => services.AddModuleDbContext<NotesDbContext>(database).AddSingleton(_log),
            bus =>
            {
                bus.AddConsumer<NoteCreatedConsumer, NoteCreatedConsumerDefinition>()
                    .Endpoint(endpoint =>
                    {
                        endpoint.Name = $"notes-sample-{Guid.NewGuid():N}";
                        endpoint.Temporary = true;
                    });
                bus.AddModuleOutbox<NotesDbContext>(outbox => outbox.QueryDelay = TimeSpan.FromMilliseconds(100));
            });
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<NotesDbContext>().Database.EnsureCreatedAsync();
        }

        await _host.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            _host.Dispose();
        }
    }

    [Fact]
    public async Task EventPublishedInATransactionIsDeliveredOnceAfterCommit()
    {
        var ct = TestContext.Current.CancellationToken;
        var noteId = Guid.NewGuid();
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            db.Notes.Add(new Note { Id = noteId, Title = "hello" });
            await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(new NoteCreated(noteId), ct);

            // Until the transaction commits the event sits in the outbox, not on the broker.
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            Assert.Equal(0, _log.Count(noteId));

            await db.SaveChangesAsync(ct);
        }

        await Eventually(() => _log.Count(noteId) > 0, _timeout, "the worker receives the event");
        await Eventually(async () => await OutboxMessagesAsync() == 0, _timeout, "the outbox is drained");
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        Assert.Equal(1, _log.Count(noteId));
        Assert.Equal(1, await ProcessedAsync(noteId));
    }

    [Fact]
    public async Task EventOfATransactionThatIsNotCommittedIsNeverDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        var noteId = Guid.NewGuid();
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            db.Notes.Add(new Note { Id = noteId, Title = "abandoned" });
            await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(new NoteCreated(noteId), ct);
        }

        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(0, _log.Total);
        Assert.Equal(0, await OutboxMessagesAsync());
    }

    [Fact]
    public async Task RedeliveredMessageIsHandledOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var noteId = Guid.NewGuid();
        var messageId = NewId.NextGuid();
        var bus = Services.GetRequiredService<IBus>();

        // The broker delivers at least once: the same message may come twice. The module's inbox drops the copy.
        await bus.Publish(new NoteCreated(noteId), context => context.MessageId = messageId, ct);
        await bus.Publish(new NoteCreated(noteId), context => context.MessageId = messageId, ct);

        await Eventually(() => _log.Count(noteId) > 0, _timeout, "the worker receives the event");
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(1, _log.Count(noteId));
        Assert.Equal(1, await ProcessedAsync(noteId));
    }

    [Fact]
    public async Task OutboxTablesLiveInTheModuleSchema()
    {
        await using var scope = Services.CreateAsyncScope();
        var tables = await scope.ServiceProvider.GetRequiredService<NotesDbContext>().Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = {0}",
                NotesDbContext.Schema)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Subset(tables.ToHashSet(), new HashSet<string> { "inbox_state", "outbox_message", "outbox_state" });
    }

    private async Task<int> OutboxMessagesAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotesDbContext>().Set<OutboxMessage>().CountAsync();
    }

    private async Task<int> ProcessedAsync(Guid noteId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotesDbContext>().ProcessedEvents
            .CountAsync(e => e.NoteId == noteId);
    }
}
