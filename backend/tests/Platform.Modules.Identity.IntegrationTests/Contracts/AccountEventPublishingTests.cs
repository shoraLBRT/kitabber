using System.Collections.Concurrent;
using System.Net;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.Contracts;
using Platform.Modules.Identity.IntegrationTests.Email;
using Platform.Modules.Identity.Passwords;

namespace Platform.Modules.Identity.IntegrationTests.Contracts;

/// <summary>
/// E18-05 (FR-ID-5, ADR-0087): Identity publishes account.created, account.email_verified, account.updated and
/// account.deleted through its outbox, as the account changes. What leaves for the bus carries no email, password or
/// token, and an account that never was one (a registration waiting for its code) is deleted silently.
/// </summary>
public sealed class AccountEventPublishingTests(EmailApi api) : IClassFixture<EmailApi>, IAsyncLifetime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private readonly ConcurrentQueue<Received> _events = new();
    private HostReceiveEndpointHandle? _endpoint;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAndListenAsync();
        _endpoint = api.Services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            $"identity-tests-account-events-{Guid.NewGuid():N}",
            (_, endpoint) =>
            {
                endpoint.Handler<AccountCreated>(c => Keep(c, AccountEventNames.Created, c.Message.AccountId));
                endpoint.Handler<AccountEmailVerified>(c => Keep(c, AccountEventNames.EmailVerified, c.Message.AccountId));
                endpoint.Handler<AccountUpdated>(c => Keep(c, AccountEventNames.Updated, c.Message.AccountId));
                endpoint.Handler<AccountDeleted>(c => Keep(c, AccountEventNames.Deleted, c.Message.AccountId));
            });
        await _endpoint.Ready;
    }

    public async ValueTask DisposeAsync()
    {
        if (_endpoint is not null)
        {
            await _endpoint.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task AccountLifeIsPublishedWithoutCredentials()
    {
        var account = await api.CreateAccountAsync();
        var first = NewAddress("first");
        await ConfirmAsync(account, first);
        var created = await EventuallyAsync(account, AccountEventNames.Created);
        Assert.Equal(api.Time.GetUtcNow(), ((AccountCreated)created.Message).OccurredAt);
        await EventuallyAsync(account, AccountEventNames.EmailVerified);

        api.Time.Advance(TimeSpan.FromHours(1));
        var second = NewAddress("second");
        await ConfirmAsync(account, second);
        var updated = (AccountUpdated)(await EventuallyAsync(account, AccountEventNames.Updated)).Message;
        Assert.Equal([AccountChanges.Email], updated.Changes);
        Assert.Equal(api.Time.GetUtcNow(), updated.OccurredAt);
        await EventuallyAsync(account, AccountEventNames.EmailVerified, count: 2);

        var hash = await SetPasswordAsync(account);
        api.Time.Advance(TimeSpan.FromHours(1));
        Assert.True(await DeleteAsync(account));
        var deleted = (AccountDeleted)(await EventuallyAsync(account, AccountEventNames.Deleted)).Message;
        Assert.Equal(api.Time.GetUtcNow(), deleted.OccurredAt);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            Assert.False(await db.Accounts.AnyAsync(a => a.Id == account, Ct));
        }

        Assert.False(await DeleteAsync(account));
        var mine = _events.Where(e => e.AccountId == account).ToList();
        Assert.Equal(
            [AccountEventNames.Created, AccountEventNames.EmailVerified, AccountEventNames.Updated,
                AccountEventNames.EmailVerified, AccountEventNames.Deleted],
            mine.Select(e => e.Name));
        Assert.All(mine, e =>
        {
            Assert.Contains($"urn:message:{e.Name}", e.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(first, e.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(second, e.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(hash, e.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("password", e.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", e.Body, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task AccountThatNeverWasOneIsDeletedSilently()
    {
        var pending = await api.CreateAccountAsync();
        Assert.True(await DeleteAsync(pending));

        // An account deleted after it was created marks the moment the outbox has delivered everything before it.
        var marker = await api.CreateAccountAsync();
        await ConfirmAsync(marker, NewAddress("marker"));
        Assert.True(await DeleteAsync(marker));
        await EventuallyAsync(marker, AccountEventNames.Deleted);

        Assert.DoesNotContain(_events, e => e.AccountId == pending);
    }

    private static string NewAddress(string name) => $"{name}.{Guid.NewGuid():N}@example.com";

    private async Task ConfirmAsync(Guid account, string email)
    {
        var code = await api.RequestCodeAsync(account, email);
        using var confirmed = await api.ConfirmAsync(account, code);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
    }

    // Gives the account a password straight in the database: its hash must never reach an event.
    private async Task<string> SetPasswordAsync(Guid account)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await db.Accounts.SingleAsync(a => a.Id == account, Ct);
        var hash = PasswordHasher.Hash("secret-1");
        stored.SetPassword(hash, endSessions: true);
        await db.SaveChangesAsync(Ct);
        return hash;
    }

    private async Task<bool> DeleteAsync(Guid account)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AccountDeletions>().DeleteAsync(account, Ct);
    }

    // Keeps the message and the envelope exactly as it came from the broker.
    private Task Keep<T>(ConsumeContext<T> context, string name, Guid account)
        where T : class
    {
        _events.Enqueue(new Received(name, account, context.ReceiveContext.Body.GetString(), context.Message));
        return Task.CompletedTask;
    }

    private async Task<Received> EventuallyAsync(Guid account, string name, int count = 1)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (true)
        {
            var found = _events.Where(e => e.AccountId == account && e.Name == name).ToList();
            if (found.Count >= count)
            {
                return found[count - 1];
            }

            Assert.True(DateTime.UtcNow < deadline, $"no {name} #{count} for {account} within {_timeout.TotalSeconds:0}s");
            await Task.Delay(100, Ct);
        }
    }

    private sealed record Received(string Name, Guid AccountId, string Body, object Message);
}
