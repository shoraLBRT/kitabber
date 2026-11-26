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

/// <summary>Two modules publish in one host, each through the outbox of its own schema (#281, ADR-0005).</summary>
public sealed class MultiModuleOutboxTests(RabbitMqFixture rabbitMq, PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private readonly ReceivedLog _log = new();
    private IHost? _host;

    private IServiceProvider Services => _host!.Services;

    public async ValueTask InitializeAsync()
    {
        var broker = rabbitMq.ConnectionString;
        var notes = await postgres.CreateDatabaseAsync();
        var tasks = await postgres.CreateDatabaseAsync();
        _host = Worker(
            RabbitMq(broker),
            services => services
                .AddModuleDbContext<NotesDbContext>(notes)
                .AddModuleDbContext<TasksDbContext>(tasks)
                .AddSingleton(_log),
            bus =>
            {
                bus.AddConsumer<NoteCreatedConsumer, NoteCreatedConsumerDefinition>().Endpoint(Temporary("notes"));
                bus.AddConsumer<TaskCreatedConsumer, TaskCreatedConsumerDefinition>().Endpoint(Temporary("tasks"));
                bus.AddModuleOutbox<NotesDbContext>(outbox => outbox.QueryDelay = TimeSpan.FromMilliseconds(100));
                bus.AddModuleOutbox<TasksDbContext>(outbox => outbox.QueryDelay = TimeSpan.FromMilliseconds(100));
            });
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<NotesDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<TasksDbContext>().Database.EnsureCreatedAsync();
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
    public async Task EachModuleWritesItsEventToItsOwnOutboxInItsTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var noteId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await using (var scope = Services.CreateAsyncScope())
        {
            var notes = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var tasks = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
            notes.Notes.Add(new Note { Id = noteId, Title = "note" });
            await scope.ServiceProvider.GetRequiredService<IModuleOutbox<NotesDbContext>>()
                .Publish(new NoteCreated(noteId), ct);
            tasks.Tasks.Add(new TaskItem { Id = taskId, Title = "task" });
            await scope.ServiceProvider.GetRequiredService<IModuleOutbox<TasksDbContext>>()
                .Publish(new TaskCreated(taskId), ct);

            // Each event waits in its own module's outbox, next to that module's changes.
            Assert.Equal([nameof(NoteCreated)], PendingEvents(notes));
            Assert.Equal([nameof(TaskCreated)], PendingEvents(tasks));

            await notes.SaveChangesAsync(ct);
            await tasks.SaveChangesAsync(ct);
        }

        await DeliveredAsync(noteId, "the notes event is delivered");
        await DeliveredAsync(taskId, "the tasks event is delivered");
        await Eventually(async () => await OutboxMessagesAsync<NotesDbContext>() == 0, _timeout, "notes' outbox drains");
        await Eventually(async () => await OutboxMessagesAsync<TasksDbContext>() == 0, _timeout, "tasks' outbox drains");
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        Assert.Equal(1, _log.Count(noteId));
        Assert.Equal(1, _log.Count(taskId));
    }

    [Fact]
    public async Task OneModuleRollingBackDoesNotLoseOrLeakTheOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var noteId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await using (var scope = Services.CreateAsyncScope())
        {
            var notes = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            notes.Notes.Add(new Note { Id = noteId, Title = "kept" });
            await scope.ServiceProvider.GetRequiredService<IModuleOutbox<NotesDbContext>>()
                .Publish(new NoteCreated(noteId), ct);
            scope.ServiceProvider.GetRequiredService<TasksDbContext>().Tasks.Add(new TaskItem { Id = taskId, Title = "dropped" });
            await scope.ServiceProvider.GetRequiredService<IModuleOutbox<TasksDbContext>>()
                .Publish(new TaskCreated(taskId), ct);

            // Only the notes module commits.
            await notes.SaveChangesAsync(ct);
        }

        await Eventually(() => _log.Count(noteId) > 0, _timeout, "the committed module's event is delivered");
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        Assert.Equal(0, _log.Count(taskId));
        Assert.Equal(0, await OutboxMessagesAsync<TasksDbContext>());
    }

    [Fact]
    public async Task ThePlainPublishEndpointOfAScopeIsTheFirstModulesOutbox()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = Services.CreateAsyncScope();
        var notes = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
        var tasks = scope.ServiceProvider.GetRequiredService<TasksDbContext>();

        await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(new NoteCreated(Guid.NewGuid()), ct);

        Assert.Equal([nameof(NoteCreated)], PendingEvents(notes));
        Assert.Empty(PendingEvents(tasks));
    }

    [Fact]
    public void RegisteringTheSameModuleOutboxTwiceIsAnError()
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() => services.AddMassTransit(bus =>
        {
            bus.AddModuleOutbox<NotesDbContext>();
            bus.AddModuleOutbox<NotesDbContext>();
        }));
        Assert.Contains(nameof(NotesDbContext), error.Message, StringComparison.Ordinal);
    }

    private static Action<IEndpointRegistrationConfigurator> Temporary(string name) => endpoint =>
    {
        endpoint.Name = $"{name}-sample-{Guid.NewGuid():N}";
        endpoint.Temporary = true;
    };

    // The events added to the context's outbox and not yet saved, by message type name.
    private static string[] PendingEvents(DbContext db) => db.ChangeTracker.Entries<OutboxMessage>()
        .Where(e => e.State == EntityState.Added)
        .Select(e => e.Entity.MessageType.Split(':')[^1])
        .ToArray();

    // On a timeout, says what each module's outbox still holds, so a red run shows which side stopped.
    private async Task DeliveredAsync(Guid id, string because)
    {
        try
        {
            await Eventually(() => _log.Count(id) > 0, _timeout, because);
        }
        catch (Xunit.Sdk.FailException failure)
        {
            Assert.Fail($"{failure.Message}; notes: {await OutboxAsync<NotesDbContext>()}; tasks: {await OutboxAsync<TasksDbContext>()}; received: {_log.Total}");
        }
    }

    private async Task<string> OutboxAsync<TContext>()
        where TContext : DbContext
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var messages = await db.Set<OutboxMessage>().CountAsync();
        var states = await db.Set<OutboxState>().Select(s => s.Delivered == null ? "pending" : "delivered").ToListAsync();
        return $"{messages} messages, outbox states [{string.Join(", ", states)}]";
    }

    private async Task<int> OutboxMessagesAsync<TContext>()
        where TContext : DbContext
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TContext>().Set<OutboxMessage>().CountAsync();
    }
}
