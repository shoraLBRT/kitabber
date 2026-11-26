using System.Globalization;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Mail;
using Platform.Messaging.IntegrationTests.SampleModule;
using Platform.Persistence;
using Platform.Testing.Containers;
using static Platform.Messaging.IntegrationTests.TestHosts;

namespace Platform.Messaging.IntegrationTests;

/// <summary>
/// The mail of a module that is not the host's first goes through its own outbox (#393, ADR-0005, item 7): the letter
/// waits in that module's schema and leaves only after that module's <c>SaveChanges</c>.
/// </summary>
[Collection(TestHosts.SampleEvents)]
public sealed class ModuleMailTests(RabbitMqFixture rabbitMq, PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private static readonly Dictionary<string, string> _values = new() { ["code"] = "482913", ["lifetime"] = "10 минут" };
    private IHost? _host;

    private IServiceProvider Services => _host!.Services;

    private InMemoryEmailSender Sender => Services.GetRequiredService<InMemoryEmailSender>();

    public async ValueTask InitializeAsync()
    {
        var notes = await postgres.CreateDatabaseAsync();
        var tasks = await postgres.CreateDatabaseAsync();
        _host = Worker(
            new Dictionary<string, string?>(RabbitMq(rabbitMq.ConnectionString)) { ["Mail:Provider"] = "InMemory" },
            services => services
                .AddModuleDbContext<NotesDbContext>(notes)
                .AddModuleDbContext<TasksDbContext>(tasks),
            bus =>
            {
                // Notes is the host's first module, so the scope's plain IPublishEndpoint is the notes outbox.
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
    public async Task SecondModulesLetterWaitsInItsOutboxAndLeavesAfterItsCommit()
    {
        var ct = TestContext.Current.CancellationToken;
        var recipient = $"{Guid.NewGuid():N}@example.org";
        await using (var scope = Services.CreateAsyncScope())
        {
            var notes = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            var tasks = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
            tasks.Tasks.Add(new TaskItem { Id = Guid.NewGuid(), Title = "task" });
            await SendAsync(scope.ServiceProvider.GetRequiredService<IMailer<TasksDbContext>>(), recipient, ct);

            Assert.Equal([nameof(SendEmail)], PendingMessages(tasks));
            Assert.Empty(PendingMessages(notes));
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            Assert.DoesNotContain(Sender.Sent, e => e.To == recipient);

            // Only the tasks module saves: its letter must not depend on the first module's commit.
            await tasks.SaveChangesAsync(ct);
        }

        await Eventually(() => Sender.Sent.Any(e => e.To == recipient), _timeout, "the tasks module's letter is delivered");
        Assert.Equal("login-code", Sender.Sent.Single(e => e.To == recipient).Template);
        await Eventually(async () => await OutboxMessagesAsync<TasksDbContext>() == 0, _timeout, "tasks' outbox drains");
        Assert.Equal(0, await OutboxMessagesAsync<NotesDbContext>());
    }

    [Fact]
    public async Task SecondModulesLetterIsDroppedWhenItDoesNotCommit()
    {
        var ct = TestContext.Current.CancellationToken;
        var dropped = $"{Guid.NewGuid():N}@example.org";
        var kept = $"{Guid.NewGuid():N}@example.org";
        await using (var scope = Services.CreateAsyncScope())
        {
            var notes = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
            scope.ServiceProvider.GetRequiredService<TasksDbContext>().Tasks.Add(new TaskItem { Id = Guid.NewGuid(), Title = "dropped" });
            await SendAsync(scope.ServiceProvider.GetRequiredService<IMailer<TasksDbContext>>(), dropped, ct);
            notes.Notes.Add(new Note { Id = Guid.NewGuid(), Title = "kept" });
            await SendAsync(scope.ServiceProvider.GetRequiredService<IMailer<NotesDbContext>>(), kept, ct);

            // Only the notes module saves: the tasks letter goes with the tasks changes, not with the notes commit.
            await notes.SaveChangesAsync(ct);
        }

        await Eventually(() => Sender.Sent.Any(e => e.To == kept), _timeout, "the committed module's letter is delivered");
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        Assert.DoesNotContain(Sender.Sent, e => e.To == dropped);
        Assert.Equal(0, await OutboxMessagesAsync<TasksDbContext>());
    }

    private static Task SendAsync(IMailer mailer, string recipient, CancellationToken ct) =>
        mailer.SendAsync(recipient, "login-code", CultureInfo.GetCultureInfo("ru-RU"), _values, ct);

    // The messages added to the context's outbox and not yet saved, by message type name.
    private static string[] PendingMessages(DbContext db) => db.ChangeTracker.Entries<OutboxMessage>()
        .Where(e => e.State == EntityState.Added)
        .Select(e => e.Entity.MessageType.Split(':')[^1])
        .ToArray();

    private async Task<int> OutboxMessagesAsync<TContext>()
        where TContext : DbContext
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TContext>().Set<OutboxMessage>().CountAsync();
    }
}
