using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Configuration;
using Platform.Modules.Identity.Contracts;
using Platform.Testing.Containers;
using Platform.Testing.Hosting;
using Platform.Workers;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E04-01: every account has a personal blog. The real workers host (<see cref="WorkerHost.CreateBuilder"/>, in-memory
/// bus) receives the Identity event <see cref="AccountCreated"/> (account.created.v1) and creates the blog once, however
/// often the event comes; a former AccountActivated still queued at the switch creates it as well (ADR-0087, item 9).
/// </summary>
public sealed class PersonalBlogActivationTests(PostgresFixture postgres) : IAsyncLifetime
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
    public async Task CreatedAccountGetsOnePersonalBlog()
    {
        var activatedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var account = Guid.CreateVersion7(activatedAt);
        var bus = _host.Services.GetRequiredService<IBus>();
        var messageId = NewId.NextGuid();

        await bus.Publish(new AccountCreated(account, activatedAt), c => c.MessageId = messageId, Ct);
        var blog = await EventuallyAsync(account);

        Assert.Equal(SubsiteType.Personal, blog.Type);
        Assert.Equal(activatedAt, blog.CreatedAt);

        // The same message again (the inbox drops it) and a second event for the account (the module ignores it).
        await bus.Publish(new AccountCreated(account, activatedAt), c => c.MessageId = messageId, Ct);
        await bus.Publish(new AccountCreated(account, activatedAt.AddMinutes(1)), Ct);
        var other = Guid.CreateVersion7(activatedAt.AddMinutes(2));
        await bus.Publish(new AccountCreated(other, activatedAt.AddMinutes(2)), Ct);
        await EventuallyAsync(other);
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        var blogs = await db.Subsites.Where(s => s.OwnerAccountId == account).ToListAsync(Ct);
        Assert.Equal(blog.Id, Assert.Single(blogs).Id);
        Assert.Equal(activatedAt, blogs[0].CreatedAt);
    }

    [Fact]
    public async Task FormerActivationStillQueuedAtTheSwitchGivesOneBlog()
    {
        var activatedAt = new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero);
        var account = Guid.CreateVersion7(activatedAt);
        var bus = _host.Services.GetRequiredService<IBus>();

        // Published before the deploy: the old event on its old exchange, then its twin account.created.
        await bus.Publish(new LegacyAccountActivated(account, activatedAt), Ct);
        var blog = await EventuallyAsync(account);
        Assert.Equal(activatedAt, blog.CreatedAt);

        await bus.Publish(new AccountCreated(account, activatedAt), Ct);
        var other = Guid.CreateVersion7(activatedAt.AddMinutes(1));
        await bus.Publish(new AccountCreated(other, activatedAt.AddMinutes(1)), Ct);
        await EventuallyAsync(other);

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        Assert.Equal(blog.Id, Assert.Single(await db.Subsites.Where(s => s.OwnerAccountId == account).ToListAsync(Ct)).Id);
    }

    [Fact]
    public void FormerActivationKeepsItsMessageTypeAndExchange()
    {
        // The drain reads only what the removed Platform.Modules.Identity.Contracts.AccountActivated left on the bus.
        Assert.Equal("urn:message:Platform.Modules.Identity.Contracts:AccountActivated",
            MessageUrn.ForTypeString<LegacyAccountActivated>());
        Assert.Equal("Platform.Modules.Identity.Contracts:AccountActivated",
            _host.Services.GetRequiredService<IBus>().Topology.Message<LegacyAccountActivated>().EntityName);
    }

    private async Task<Subsite> EventuallyAsync(Guid account)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (true)
        {
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
                if (await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.OwnerAccountId == account, Ct) is { } blog)
                {
                    return blog;
                }
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"no personal blog for {account} after {_timeout.TotalSeconds:0}s");
            }

            await Task.Delay(100, Ct);
        }
    }
}
