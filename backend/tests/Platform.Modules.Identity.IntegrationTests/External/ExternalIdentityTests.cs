using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.External;
using Platform.Modules.Identity.IntegrationTests.SignIn;

namespace Platform.Modules.Identity.IntegrationTests.External;

/// <summary>
/// External identities (FR-ID-4, E18-04, ADR-0098) on a real database: a pair «provider, subject» belongs to at most
/// one account, every link and unlink is in the journal, the last way to sign in cannot be unlinked, and the links go
/// with the deleted account. The API lists and unlinks only the signed-in account's own identities.
/// </summary>
public sealed class ExternalIdentityTests(IdentityApi api) : IClassFixture<IdentityApi>, IAsyncLifetime
{
    private const string _path = "/api/v1/account/identities";

    private readonly List<HttpClient> _browsers = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetWithClientsAsync();

    public ValueTask DisposeAsync()
    {
        _browsers.ForEach(b => b.Dispose());
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PairIsLinkedToAtMostOneAccount()
    {
        var first = await api.CreateAccountAsync();
        var second = await api.CreateAccountAsync();
        var subject = Subject();

        Assert.Equal(LinkOutcome.Linked, await LinkAsync(first.Id, ExternalProviders.Yandex, subject));
        Assert.Equal(LinkOutcome.TakenByAnotherAccount, await LinkAsync(second.Id, ExternalProviders.Yandex, subject));
        Assert.Equal(LinkOutcome.AlreadyLinked, await LinkAsync(first.Id, ExternalProviders.Yandex, subject));
        // One identity of each provider per account; the same subject at another provider is another person.
        Assert.Equal(LinkOutcome.ProviderAlreadyLinked, await LinkAsync(first.Id, ExternalProviders.Yandex, Subject()));
        Assert.Equal(LinkOutcome.Linked, await LinkAsync(second.Id, ExternalProviders.Vk, subject));
        Assert.Equal(LinkOutcome.AccountNotFound, await LinkAsync(Guid.CreateVersion7(), ExternalProviders.Google, Subject()));

        await using var scope = api.Services.CreateAsyncScope();
        var identities = scope.ServiceProvider.GetRequiredService<ExternalIdentities>();
        Assert.Equal(first.Id, await identities.FindAccountAsync(ExternalProviders.Yandex, subject, Ct));
        Assert.Equal(second.Id, await identities.FindAccountAsync(ExternalProviders.Vk, subject, Ct));
        Assert.Null(await identities.FindAccountAsync(ExternalProviders.Yandex, subject.ToUpperInvariant(), Ct));
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await db.ExternalIdentities.CountAsync(
            i => i.Provider == ExternalProviders.Yandex && i.Subject == subject, Ct));
    }

    [Fact]
    public async Task PairLinkedToTwoAccountsAtOnceEndsInOne()
    {
        var accounts = new[] { await api.CreateAccountAsync(), await api.CreateAccountAsync() };
        var subject = Subject();

        var outcomes = await Task.WhenAll(accounts.Select(a => LinkAsync(a.Id, ExternalProviders.Telegram, subject)));

        Assert.Single(outcomes, o => o == LinkOutcome.Linked);
        Assert.Single(outcomes, o => o == LinkOutcome.TakenByAnotherAccount);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Single(await db.ExternalIdentities.Where(i => i.Subject == subject).ToListAsync(Ct));
        Assert.Single(await db.ExternalIdentityLog.Where(e => e.Subject == subject).ToListAsync(Ct));
    }

    [Theory]
    [InlineData("Yandex", "id-1")]
    [InlineData("y", "id-1")]
    [InlineData("1vk", "id-1")]
    [InlineData("yandex", "")]
    [InlineData("yandex", " id-1")]
    public async Task MalformedProviderOrSubjectIsRefused(string provider, string subject)
    {
        var account = await api.CreateAccountAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => LinkAsync(account.Id, provider, subject));
        Assert.False(ExternalProviders.IsValid(provider) && ExternalProviders.IsValidSubject(subject));
    }

    [Fact]
    public async Task LinkingAndUnlinkingAreWrittenToTheJournal()
    {
        var account = await CreateConfirmedAccountAsync();
        var subject = Subject();
        var linkedAt = api.Time.GetUtcNow();
        await LinkAsync(account.Id, ExternalProviders.Google, subject);
        await LinkAsync(account.Id, ExternalProviders.Google, subject);
        api.Time.Advance(TimeSpan.FromMinutes(3));
        var unlinkedAt = api.Time.GetUtcNow();

        Assert.Equal(UnlinkOutcome.Unlinked, await UnlinkAsync(account.Id, (await ListAsync(account.Id)).Single().Id));

        await using var scope = api.Services.CreateAsyncScope();
        var log = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().ExternalIdentityLog
            .Where(e => e.AccountId == account.Id).OrderBy(e => e.At).ToListAsync(Ct);
        Assert.Collection(log,
            e => Assert.Equal((ExternalIdentityAction.Linked, ExternalProviders.Google, subject, linkedAt),
                (e.Action, e.Provider, e.Subject, e.At)),
            e => Assert.Equal((ExternalIdentityAction.Unlinked, ExternalProviders.Google, subject, unlinkedAt),
                (e.Action, e.Provider, e.Subject, e.At)));
        Assert.Empty(await ListAsync(account.Id));
    }

    [Fact]
    public async Task LastWayToSignInCannotBeUnlinked()
    {
        // No email: the providers are the only ways in.
        var account = await api.CreateAccountAsync();
        await LinkAsync(account.Id, ExternalProviders.Vk, Subject());
        var vk = (await ListAsync(account.Id)).Single().Id;
        Assert.Equal(UnlinkOutcome.LastSignInMethod, await UnlinkAsync(account.Id, vk));

        await LinkAsync(account.Id, ExternalProviders.Telegram, Subject());
        Assert.Equal(UnlinkOutcome.Unlinked, await UnlinkAsync(account.Id, vk));
        var telegram = (await ListAsync(account.Id)).Single().Id;
        Assert.Equal(UnlinkOutcome.LastSignInMethod, await UnlinkAsync(account.Id, telegram));

        // A confirmed email signs in by code or password: the only provider can go.
        var withEmail = await CreateConfirmedAccountAsync();
        await LinkAsync(withEmail.Id, ExternalProviders.Yandex, Subject());
        Assert.Equal(UnlinkOutcome.Unlinked, await UnlinkAsync(withEmail.Id, (await ListAsync(withEmail.Id)).Single().Id));
    }

    [Fact]
    public async Task UnlinksOfTheLastTwoIdentitiesAtOnceLeaveOne()
    {
        var account = await api.CreateAccountAsync();
        await LinkAsync(account.Id, ExternalProviders.Vk, Subject());
        await LinkAsync(account.Id, ExternalProviders.Google, Subject());
        var ids = (await ListAsync(account.Id)).Select(i => i.Id).ToArray();

        var outcomes = await Task.WhenAll(ids.Select(id => UnlinkAsync(account.Id, id)));

        Assert.Single(outcomes, o => o == UnlinkOutcome.Unlinked);
        Assert.Single(outcomes, o => o == UnlinkOutcome.LastSignInMethod);
        Assert.Single(await ListAsync(account.Id));
    }

    [Fact]
    public async Task DeletedAccountTakesItsLinksAndJournalAndFreesThePair()
    {
        var account = await CreateConfirmedAccountAsync();
        var subject = Subject();
        await LinkAsync(account.Id, ExternalProviders.Yandex, subject);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<AccountDeletions>().DeleteAsync(account.Id, Ct));
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            Assert.False(await db.ExternalIdentities.AnyAsync(i => i.AccountId == account.Id, Ct));
            Assert.False(await db.ExternalIdentityLog.AnyAsync(e => e.AccountId == account.Id, Ct));
        }

        var next = await api.CreateAccountAsync();
        Assert.Equal(LinkOutcome.Linked, await LinkAsync(next.Id, ExternalProviders.Yandex, subject));
    }

    [Fact]
    public async Task OwnerListsAndUnlinksTheirIdentitiesOverTheApi()
    {
        var browser = Browser();
        var (account, tokens) = await OidcFlow.SignInAsync(api, browser);
        var accessToken = tokens.GetProperty("access_token").GetString()!;
        await LinkAsync(account.Id, ExternalProviders.Yandex, Subject());
        api.Time.Advance(TimeSpan.FromSeconds(1));
        await LinkAsync(account.Id, ExternalProviders.Telegram, Subject());

        using (var list = await SendAsync(HttpMethod.Get, _path, accessToken))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var items = (await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray().ToArray();
            Assert.Equal(new string?[] { ExternalProviders.Yandex, ExternalProviders.Telegram },
                items.Select(i => i.GetProperty("provider").GetString()).ToArray());
            // The subject stays inside Identity.
            Assert.All(items, i => Assert.False(i.TryGetProperty("subject", out _)));
        }

        var yandex = (await ListAsync(account.Id))[0].Id;
        using (var unlinked = await SendAsync(HttpMethod.Delete, $"{_path}/{yandex}", accessToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);
        }

        var telegram = Assert.Single(await ListAsync(account.Id)).Id;
        using var refused = await SendAsync(HttpMethod.Delete, $"{_path}/{telegram}", accessToken);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(ExternalIdentityEndpoints.LastSignInMethodCode, await CodeOf(refused));
    }

    [Fact]
    public async Task GuestsAndOtherAccountsCannotSeeOrUnlinkIdentities()
    {
        var owner = await CreateConfirmedAccountAsync();
        await LinkAsync(owner.Id, ExternalProviders.Vk, Subject());
        var ownerIdentity = (await ListAsync(owner.Id)).Single().Id;
        var (_, tokens) = await OidcFlow.SignInAsync(api, Browser());
        var otherToken = tokens.GetProperty("access_token").GetString()!;

        using (var guest = await SendAsync(HttpMethod.Get, _path, accessToken: null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        }

        using (var guestUnlink = await SendAsync(HttpMethod.Delete, $"{_path}/{ownerIdentity}", accessToken: null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, guestUnlink.StatusCode);
        }

        using (var others = await SendAsync(HttpMethod.Get, _path, otherToken))
        {
            Assert.Empty((await others.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray());
        }

        // Another account's identity is answered as a missing one, and stays linked.
        using (var foreign = await SendAsync(HttpMethod.Delete, $"{_path}/{ownerIdentity}", otherToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Equal(ExternalIdentityEndpoints.NotFoundCode, await CodeOf(foreign));
        }

        using (var malformed = await SendAsync(HttpMethod.Delete, $"{_path}/not-an-id", otherToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        }

        Assert.Single(await ListAsync(owner.Id));
    }

    private static string Subject() => Guid.NewGuid().ToString("N");

    private async Task<Account> CreateConfirmedAccountAsync()
    {
        var account = Account.Create(api.Time);
        var email = $"{Guid.NewGuid():N}@example.com";
        account.ConfirmEmail(email, email, api.Time.GetUtcNow());
        await api.SaveAsync<IdentityDbContext>(account);
        return account;
    }

    private async Task<LinkOutcome> LinkAsync(Guid accountId, string provider, string subject)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ExternalIdentities>()
            .LinkAsync(accountId, provider, subject, Ct);
    }

    private async Task<UnlinkOutcome> UnlinkAsync(Guid accountId, Guid identityId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ExternalIdentities>()
            .UnlinkAsync(accountId, identityId, Ct);
    }

    private async Task<ExternalIdentityView[]> ListAsync(Guid accountId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ExternalIdentities>().ListAsync(accountId, Ct);
    }

    private HttpClient Browser()
    {
        var browser = api.CreateBrowser();
        _browsers.Add(browser);
        return browser;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }

        return await api.CreateClient().SendAsync(request, Ct);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
