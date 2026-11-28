using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Configuration;
using Platform.Modules.Blogs.Topics;
using Platform.Modules.Identity.Contracts;
using Platform.Testing.Containers;
using Platform.Testing.Hosting;
using Platform.Workers;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// #462 (FR-ID-5, ADR-0087, ADR-0121): when an account is deleted, the real workers host (in-memory bus) receives
/// account.deleted and erases its appointments as a topic moderator and the transfer offers it made or received; a
/// repeat does no harm, and the rows of other accounts stay.
/// </summary>
public sealed class TopicRolesErasureTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private IHost _host = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var database = await postgres.CreateDatabaseAsync();
        _host = WorkerHost.CreateBuilder(
            ["--environment", PlatformEnvironments.Test, "--Messaging:Transport", "InMemory", "--Cache:Provider", "InMemory",
                "--Mail:Provider", "InMemory", $"--ConnectionStrings:Platform={database}"]).Build();
        await HostStartGate.RunAsync(async () =>
        {
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<BlogsDbContext>().Database.MigrateAsync(Ct);
            }

            await _host.StartAsync(Ct);
        }, Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        _host.Dispose();
    }

    [Fact]
    public async Task DeletedAccountLosesItsModeratorRolesAndTransferOffers()
    {
        var deletedAt = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var account = Guid.CreateVersion7(deletedAt.AddDays(-30));
        var owner = Guid.CreateVersion7(deletedAt.AddDays(-20));
        var other = Guid.CreateVersion7(deletedAt.AddDays(-10));
        var offeredToAccount = Topic("Наука", owner, deletedAt);
        var offeredByAccount = Topic("Кино", account, deletedAt);
        var untouched = Topic("Книги", owner, deletedAt);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
            db.Subsites.AddRange(offeredToAccount, offeredByAccount, untouched);
            db.TopicModerators.AddRange(
                new TopicModerator { SubsiteId = offeredToAccount.Id, AccountId = account, AppointedAt = deletedAt },
                new TopicModerator { SubsiteId = untouched.Id, AccountId = account, AppointedAt = deletedAt },
                new TopicModerator { SubsiteId = untouched.Id, AccountId = other, AppointedAt = deletedAt });
            db.TopicTransferOffers.AddRange(
                new TopicTransferOffer
                {
                    SubsiteId = offeredToAccount.Id,
                    FromAccountId = owner,
                    ToAccountId = account,
                    OfferedAt = deletedAt,
                },
                new TopicTransferOffer
                {
                    SubsiteId = offeredByAccount.Id,
                    FromAccountId = account,
                    ToAccountId = other,
                    OfferedAt = deletedAt,
                },
                new TopicTransferOffer
                {
                    SubsiteId = untouched.Id,
                    FromAccountId = owner,
                    ToAccountId = other,
                    OfferedAt = deletedAt,
                });
            await db.SaveChangesAsync(Ct);
        }

        var bus = _host.Services.GetRequiredService<IBus>();
        var messageId = NewId.NextGuid();
        await bus.Publish(new AccountDeleted(account, deletedAt), c => c.MessageId = messageId, Ct);
        await GoneAsync(account);

        // The same message again (the inbox drops it) and a repeat with a new id change nothing more.
        await bus.Publish(new AccountDeleted(account, deletedAt), c => c.MessageId = messageId, Ct);
        await bus.Publish(new AccountDeleted(account, deletedAt.AddMinutes(1)), Ct);
        await bus.Publish(new AccountDeleted(Guid.CreateVersion7(deletedAt), deletedAt), Ct);

        await using var check = _host.Services.CreateAsyncScope();
        var stored = check.ServiceProvider.GetRequiredService<BlogsDbContext>();
        var moderators = await stored.TopicModerators.AsNoTracking()
            .Select(m => new { m.SubsiteId, m.AccountId }).ToListAsync(Ct);
        Assert.Equal([new { SubsiteId = untouched.Id, AccountId = other }], moderators);
        var offers = await stored.TopicTransferOffers.AsNoTracking().Select(o => o.SubsiteId).ToListAsync(Ct);
        Assert.Equal([untouched.Id], offers);
    }

    private static Subsite Topic(string name, Guid owner, DateTimeOffset at) =>
        Subsite.CreateTopic(name, $"t{Guid.CreateVersion7().ToString("N")[^12..]}", owner, at);

    private async Task GoneAsync(Guid account)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (true)
        {
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
                if (!await db.TopicModerators.AnyAsync(m => m.AccountId == account, Ct)
                    && !await db.TopicTransferOffers.AnyAsync(o => o.FromAccountId == account || o.ToAccountId == account, Ct))
                {
                    return;
                }
            }

            Assert.True(DateTime.UtcNow < deadline, $"the topic roles of {account} are still there after {_timeout.TotalSeconds:0}s");
            await Task.Delay(100, Ct);
        }
    }
}
