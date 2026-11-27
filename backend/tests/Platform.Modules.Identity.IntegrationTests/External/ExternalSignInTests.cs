using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.External;
using Platform.Modules.Identity.IntegrationTests.SignIn;
using Platform.Modules.Identity.SignIn;
using Platform.Testing.Containers;

namespace Platform.Modules.Identity.IntegrationTests.External;

/// <summary>The Identity API with every external provider replaced by the fake (<see cref="ExternalSignInSettings.FakeKey"/>).</summary>
public sealed class ExternalSignInApi : IdentityApi
{
    public ExternalSignInApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithSetting(ExternalSignInSettings.FakeKey, "true");
}

/// <summary>
/// Signing in with an external provider (FR-AUTH, #91, ADR-0115) through the API and the fake provider, a browser hop
/// by hop: the pair finds its account, a confirmed email finds or creates one, a provider without one signs in only to
/// an account it is linked to; linking goes to the account signed in at the provider; every failure goes back with
/// <c>externalError</c> and no session.
/// </summary>
public sealed class ExternalSignInTests(ExternalSignInApi api) : IClassFixture<ExternalSignInApi>, IAsyncLifetime
{
    private const string _start = "/api/v1/sign-in/external/";

    private readonly List<HttpClient> _browsers = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetWithClientsAsync();

    public ValueTask DisposeAsync()
    {
        _browsers.ForEach(b => b.Dispose());
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ListNamesTheProvidersTurnedOn()
    {
        var providers = await api.CreateClient().GetFromJsonAsync<JsonElement>(_start.TrimEnd('/'), Ct);

        Assert.Equal(["yandex", "vk", "google", "telegram"],
            providers.EnumerateArray().Select(p => p.GetProperty("key").GetString()));
    }

    [Fact]
    public async Task NewPersonWithAConfirmedEmailGetsAnAccountWithItAndSignsInAgainByTheSubject()
    {
        var email = $"Reader.{Guid.NewGuid():N}@example.com";
        var browser = Browser();

        var landing = await SignInAsync(browser, ExternalProviders.Google, "google-1", email);

        Assert.Equal("/auth/sign-in?returnTo=/", landing.PathAndQuery);
        var accountId = await WhoAmIAsync(browser);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var account = await db.Accounts.SingleAsync(a => a.Id == accountId, Ct);
            Assert.Equal(email, account.Email);
            Assert.True(account.HasConfirmedEmail);
            Assert.Null(account.PasswordHash);
            var identity = await db.ExternalIdentities.SingleAsync(i => i.AccountId == accountId, Ct);
            Assert.Equal(("google", "google-1"), (identity.Provider, identity.Subject));
            Assert.Equal(1, await db.ExternalIdentityLog.CountAsync(e => e.AccountId == accountId, Ct));
        }

        // The pair signs in again, whatever email the provider gives now.
        var again = Browser();
        await SignInAsync(again, ExternalProviders.Google, "google-1", email: null);
        Assert.Equal(accountId, await WhoAmIAsync(again));
    }

    [Fact]
    public async Task ConfirmedEmailOfAnAccountLinksTheProviderToIt()
    {
        var email = $"owner.{Guid.NewGuid():N}@example.com";
        var account = await AccountWithEmailAsync(email);
        var browser = Browser();

        await SignInAsync(browser, ExternalProviders.Yandex, "yandex-7", email.ToUpperInvariant());

        Assert.Equal(account, await WhoAmIAsync(browser));
        Assert.Equal("yandex", Assert.Single(await IdentitiesAsync(account)).Provider);
    }

    [Fact]
    public async Task EmailTheProviderDoesNotVouchForAndNoEmailAtAllSignInNobody()
    {
        var email = $"victim.{Guid.NewGuid():N}@example.com";
        var victim = await AccountWithEmailAsync(email);

        foreach (var (provider, address, verified) in new[]
                 {
                     (ExternalProviders.Vk, email, false), (ExternalProviders.Telegram, (string?)null, false),
                 })
        {
            var browser = Browser();
            var location = await CallbackAsync(browser, await ProviderAsync(browser, provider, "subject-1", address, verified));
            Assert.Equal("/login?externalError=email_required", location.PathAndQuery);
            Assert.Null(await SessionCookieAsync(browser));
        }

        Assert.Empty(await IdentitiesAsync(victim));
        await using var scope = api.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.CountAsync(Ct));
    }

    [Fact]
    public async Task ProviderWithoutEmailSignsInToTheAccountItIsLinkedTo()
    {
        var account = await api.CreateAccountAsync();
        var browser = Browser();
        await OidcFlow.SignInAtProviderAsync(browser, account.Id);
        Assert.Equal("/settings/sign-in", (await LinkAsync(browser, ExternalProviders.Telegram, "100500")).PathAndQuery);

        var later = Browser();
        await SignInAsync(later, ExternalProviders.Telegram, "100500", email: null);

        Assert.Equal(account.Id, await WhoAmIAsync(later));
    }

    [Fact]
    public async Task LinkingKeepsThePairOfAnotherAccountAndNeedsASession()
    {
        var owner = await api.CreateAccountAsync();
        var other = await api.CreateAccountAsync();
        var ownerBrowser = Browser();
        await OidcFlow.SignInAtProviderAsync(ownerBrowser, owner.Id);
        await LinkAsync(ownerBrowser, ExternalProviders.Vk, "vk-1");

        var otherBrowser = Browser();
        await OidcFlow.SignInAtProviderAsync(otherBrowser, other.Id);
        Assert.Equal("/settings/sign-in?externalError=taken_by_another_account",
            (await LinkAsync(otherBrowser, ExternalProviders.Vk, "vk-1")).PathAndQuery);
        Assert.Equal("/settings/sign-in?externalError=provider_already_linked",
            (await LinkAsync(ownerBrowser, ExternalProviders.Vk, "vk-2")).PathAndQuery);
        Assert.Empty(await IdentitiesAsync(other.Id));

        using var anonymous = await Browser().GetAsync(_start + "vk?link=true&returnUrl=/settings/sign-in", Ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal("/settings/sign-in?externalError=session_required", anonymous.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task RefusalWrongStateAndMissingAttemptSignInNobody()
    {
        var denied = Browser();
        var authorize = await StartAsync(denied, ExternalProviders.Google);
        using (var deny = await denied.GetAsync(authorize.PathAndQuery + "&deny=1", Ct))
        {
            Assert.Equal("/login?externalError=denied", (await CallbackAsync(denied, deny.Headers.Location!)).PathAndQuery);
        }

        var forged = Browser();
        var provider = await ProviderAsync(forged, ExternalProviders.Google, "google-9", "x@example.com");
        var wrongState = new Uri(provider.GetLeftPart(UriPartial.Path) + "?code="
            + OidcFlow.Query(provider)["code"] + "&state=forged");
        Assert.Equal("/login?externalError=expired", (await CallbackAsync(forged, wrongState)).PathAndQuery);

        // The attempt's cookie is spent by the first callback.
        Assert.Equal("/login?externalError=expired", (await CallbackAsync(forged, provider)).PathAndQuery);
        // A provider that does not match the attempt.
        var mismatched = Browser();
        var yandex = await ProviderAsync(mismatched, ExternalProviders.Yandex, "y-1", "y@example.com");
        var asGoogle = new Uri(yandex.AbsoluteUri.Replace("/yandex/", "/google/", StringComparison.Ordinal));
        Assert.Equal("/login?externalError=expired", (await CallbackAsync(mismatched, asGoogle)).PathAndQuery);

        foreach (var browser in new[] { denied, forged, mismatched })
        {
            Assert.Null(await SessionCookieAsync(browser));
        }

        await using var scope = api.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.CountAsync(Ct));
    }

    [Fact]
    public async Task OnlyAddressesOfTheSiteAreReturnedTo()
    {
        var browser = Browser();
        foreach (var refused in new[] { "//evil.example/", "/\\evil.example", "https://evil.example/", "javascript:alert(1)" })
        {
            using var response = await browser.GetAsync(_start + "google?returnUrl=" + Uri.EscapeDataString(refused), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var unknown = await browser.GetAsync(_start + "github", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // The site's own origin (its client's redirect address) is accepted, as is a path of the API's origin.
        var site = "http://localhost:3000/auth/sign-in?returnTo=%2Fpost%2F1";
        var landing = await CallbackAsync(browser, await ProviderAsync(
            browser, ExternalProviders.Google, "google-3", "site@example.com", returnUrl: site));
        Assert.Equal(site, landing.AbsoluteUri);
    }

    private HttpClient Browser()
    {
        var browser = api.CreateBrowser();
        _browsers.Add(browser);
        return browser;
    }

    // The fake provider's sign-in page the start sends the browser to.
    private static async Task<Uri> StartAsync(HttpClient browser, string provider, string query = "")
    {
        using var start = await browser.GetAsync(_start + provider + query, Ct);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var location = start.Headers.Location!;
        Assert.Contains("/sign-in/external/fake/" + provider + "/authorize", location.AbsolutePath, StringComparison.Ordinal);
        return location;
    }

    // The callback address the fake provider sends the browser back to, for the person named.
    private static async Task<Uri> ProviderAsync(
        HttpClient browser, string provider, string subject, string? email, bool verified = true, string? returnUrl = null,
        bool link = false)
    {
        var query = new List<string>();
        if (returnUrl is not null)
        {
            query.Add("returnUrl=" + Uri.EscapeDataString(returnUrl));
        }

        if (link)
        {
            query.Add("link=true");
        }

        var authorize = await StartAsync(browser, provider, query.Count > 0 ? "?" + string.Join('&', query) : string.Empty);
        using var page = await browser.GetAsync(authorize.PathAndQuery, Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var person = $"&subject={Uri.EscapeDataString(subject)}&email={Uri.EscapeDataString(email ?? string.Empty)}"
            + (verified ? "&verified=true" : string.Empty);
        using var signedIn = await browser.GetAsync(authorize.PathAndQuery + person, Ct);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        return signedIn.Headers.Location!;
    }

    private static async Task<Uri> CallbackAsync(HttpClient browser, Uri callback)
    {
        using var response = await browser.GetAsync(callback.PathAndQuery, Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return new Uri(new Uri("http://localhost/"), response.Headers.Location!);
    }

    private static async Task<Uri> SignInAsync(HttpClient browser, string provider, string subject, string? email) =>
        await CallbackAsync(browser, await ProviderAsync(browser, provider, subject, email));

    private static async Task<Uri> LinkAsync(HttpClient browser, string provider, string subject) =>
        await CallbackAsync(browser, await ProviderAsync(
            browser, provider, subject, email: null, verified: false, returnUrl: "/settings/sign-in", link: true));

    // The account the browser's provider session signs in to, through the site's authorization code flow.
    private static async Task<Guid> WhoAmIAsync(HttpClient browser)
    {
        var pkce = Pkce.Create();
        var code = await OidcFlow.CodeAsync(browser, pkce);
        var (status, tokens) = await OidcFlow.ExchangeAsync(browser, code, pkce.Verifier);
        Assert.Equal(HttpStatusCode.OK, status);
        using var whoAmI = await OidcFlow.WhoAmIAsync(browser, tokens.GetProperty("access_token").GetString());
        return Guid.Parse(await whoAmI.Content.ReadAsStringAsync(Ct));
    }

    // The redirect of /connect/authorize: to the sign-in page without a provider session, null then.
    private static async Task<string?> SessionCookieAsync(HttpClient browser)
    {
        using var authorize = await browser.GetAsync(OidcFlow.AuthorizeUrl(Pkce.Create()), Ct);
        return authorize.Headers.Location!.OriginalString.Contains(IdentitySettings.DefaultSignInPath + "?", StringComparison.Ordinal)
            ? null
            : authorize.Headers.Location.OriginalString;
    }

    private async Task<Guid> AccountWithEmailAsync(string email)
    {
        var account = Account.Create(api.Time);
        account.ConfirmEmail(email, Platform.Modules.Identity.Email.EmailAddresses.Normalize(email)!, api.Time.GetUtcNow());
        await api.SaveAsync<IdentityDbContext>(account);
        return account.Id;
    }

    private async Task<ExternalIdentityView[]> IdentitiesAsync(Guid accountId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ExternalIdentities>().ListAsync(accountId, Ct);
    }
}
