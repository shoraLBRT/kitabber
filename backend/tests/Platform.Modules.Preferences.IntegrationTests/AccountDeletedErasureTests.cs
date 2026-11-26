using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Configuration;
using Platform.Modules.Identity.Contracts;
using Platform.Testing.Containers;
using Platform.Workers;

namespace Platform.Modules.Preferences.IntegrationTests;

/// <summary>
/// E18-05 (FR-ID-5, ADR-0087): a module erases what it keeps by the account_id when the account is deleted. The real
/// workers host (<see cref="WorkerHost.CreateBuilder"/>, in-memory bus) receives account.deleted and removes the
/// account's settings; a repeat and an account without settings do no harm, and other accounts keep theirs.
/// </summary>
public sealed class AccountDeletedErasureTests(PostgresFixture postgres) : IAsyncLifetime
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
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PreferencesDbContext>().Database.MigrateAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<Blogs.BlogsDbContext>().Database.MigrateAsync(Ct);
        }

        await _host.StartAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        _host.Dispose();
    }

    [Fact]
    public async Task DeletedAccountLosesItsSettings()
    {
        var deletedAt = new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
        var account = Guid.CreateVersion7(deletedAt.AddDays(-30));
        var other = Guid.CreateVersion7(deletedAt.AddDays(-20));
        var marker = Guid.CreateVersion7(deletedAt.AddDays(-10));
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PreferencesDbContext>();
            db.AccountPreferences.AddRange(
                new AccountPreferences { AccountId = account, Theme = ThemeChoice.Dark, UpdatedAt = deletedAt },
                new AccountPreferences { AccountId = other, Theme = ThemeChoice.Light, UpdatedAt = deletedAt },
                new AccountPreferences { AccountId = marker, Theme = ThemeChoice.Light, UpdatedAt = deletedAt });
            await db.SaveChangesAsync(Ct);
        }

        var bus = _host.Services.GetRequiredService<IBus>();
        var messageId = NewId.NextGuid();
        await bus.Publish(new AccountDeleted(account, deletedAt), c => c.MessageId = messageId, Ct);
        await GoneAsync(account);

        // The same message again (the inbox drops it), a repeat with a new id and an account without settings.
        await bus.Publish(new AccountDeleted(account, deletedAt), c => c.MessageId = messageId, Ct);
        await bus.Publish(new AccountDeleted(account, deletedAt.AddMinutes(1)), Ct);
        await bus.Publish(new AccountDeleted(Guid.CreateVersion7(deletedAt), deletedAt), Ct);
        await bus.Publish(new AccountDeleted(marker, deletedAt), Ct);
        await GoneAsync(marker);

        await using var check = _host.Services.CreateAsyncScope();
        var stored = await check.ServiceProvider.GetRequiredService<PreferencesDbContext>().AccountPreferences
            .AsNoTracking().Select(p => p.AccountId).ToListAsync(Ct);
        Assert.Equal([other], stored);
    }

    private async Task GoneAsync(Guid account)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (true)
        {
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PreferencesDbContext>();
                if (!await db.AccountPreferences.AnyAsync(p => p.AccountId == account, Ct))
                {
                    return;
                }
            }

            Assert.True(DateTime.UtcNow < deadline, $"the settings of {account} are still there after {_timeout.TotalSeconds:0}s");
            await Task.Delay(100, Ct);
        }
    }
}
