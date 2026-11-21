using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.SignIn;
using static Platform.Modules.Identity.IntegrationTests.SignIn.OidcFlow;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>
/// The site's session over OpenID Connect (E03-03, ADR-0024) on a real database: a refresh token renews an expired
/// access token, every refresh rotates it, a rotated one reused after the leeway revokes the whole chain, revocation
/// (sign-out) ends it, and the end-session endpoint ends the person's session at the provider.
/// </summary>
public sealed class SessionTokenTests(IdentityApi api) : IClassFixture<IdentityApi>, IAsyncLifetime
{
    private const string _siteHome = "http://localhost:3000/";

    private HttpClient _browser = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetWithClientsAsync();
        _browser = api.CreateBrowser();
    }

    public ValueTask DisposeAsync()
    {
        _browser.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RefreshTokenIsIssuedOnlyForOfflineAccess()
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);
        var pkce = Pkce.Create();
        var result = await AuthorizeAsync(_browser, AuthorizeUrl(pkce, scope: "openid"));

        var (status, tokens) = await ExchangeAsync(_browser, result["code"], pkce.Verifier);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(tokens.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task ExpiredAccessTokenIsRenewedByTheRefreshToken()
    {
        var (account, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();
        api.Time.Advance(TimeSpan.FromMinutes(16));
        using var expired = await WhoAmIAsync(client, Access(tokens));

        var (status, renewed) = await RefreshAsync(client, Refresh(tokens));

        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEqual(Refresh(tokens), Refresh(renewed));
        Assert.Equal(account.Id.ToString("D"), Decode(renewed.GetProperty("id_token").GetString()!).Payload.GetProperty("sub").GetString());
        using var signedIn = await WhoAmIAsync(client, Access(renewed));
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(account.Id.ToString("D"), await signedIn.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task EachRefreshRotatesTheTokenAndExtendsTheSession()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();
        var refresh = Refresh(tokens);

        // Three weeks apart, twice: longer than the lifetime in all, yet every rotation starts it again.
        for (var i = 0; i < 2; i++)
        {
            api.Time.Advance(TimeSpan.FromDays(21));
            var (status, renewed) = await RefreshAsync(client, refresh);
            Assert.Equal(HttpStatusCode.OK, status);
            refresh = Refresh(renewed);
        }
    }

    [Fact]
    public async Task UnusedRefreshTokenExpires()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();
        api.Time.Advance(IdentityModule.RefreshTokenLifetime + TimeSpan.FromMinutes(1));

        var (status, body) = await RefreshAsync(client, Refresh(tokens));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ReusedRotatedRefreshTokenRevokesTheWholeChain()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();
        var (_, rotated) = await RefreshAsync(client, Refresh(tokens));
        api.Time.Advance(IdentityModule.RefreshTokenReuseLeeway + TimeSpan.FromSeconds(1));

        // The old token again: a stolen copy, whoever holds it.
        var (reuse, reuseBody) = await RefreshAsync(client, Refresh(tokens));
        // The newest token of the same chain is revoked with it: neither the thief nor the person can go on.
        var (latest, latestBody) = await RefreshAsync(client, Refresh(rotated));

        Assert.Equal(HttpStatusCode.BadRequest, reuse);
        Assert.Equal("invalid_grant", reuseBody.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, latest);
        Assert.Equal("invalid_grant", latestBody.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ReuseOfAnotherChainLeavesItAlone()
    {
        var (_, first) = await SignInAsync(api, _browser);
        using var otherBrowser = api.CreateBrowser();
        var (_, second) = await SignInAsync(api, otherBrowser);
        using var client = api.CreateClient();
        await RefreshAsync(client, Refresh(first));
        api.Time.Advance(IdentityModule.RefreshTokenReuseLeeway + TimeSpan.FromSeconds(1));
        await RefreshAsync(client, Refresh(first));

        var (status, _) = await RefreshAsync(client, Refresh(second));

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task RotatedRefreshTokenStillWorksWithinTheLeeway()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();
        var (_, rotated) = await RefreshAsync(client, Refresh(tokens));

        // Two requests of one browser refreshing at once: the second one is no theft.
        var (concurrent, _) = await RefreshAsync(client, Refresh(tokens));
        var (latest, _) = await RefreshAsync(client, Refresh(rotated));

        Assert.Equal(HttpStatusCode.OK, concurrent);
        Assert.Equal(HttpStatusCode.OK, latest);
    }

    [Fact]
    public async Task RevokedRefreshTokenGivesNoTokens()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();

        var revoked = await RevokeAsync(client, Refresh(tokens));
        var (status, body) = await RefreshAsync(client, Refresh(tokens));

        Assert.Equal(HttpStatusCode.OK, revoked);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RefreshTokenOfOneClientIsRefusedToAnother()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();

        var (status, body) = await RefreshAsync(client, Refresh(tokens), "admin");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RefreshTokenOfADeletedAccountIsRefused()
    {
        var (account, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Accounts.Where(a => a.Id == account.Id).ExecuteDeleteAsync(Ct);
        }

        var (status, body) = await RefreshAsync(client, Refresh(tokens));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task EndSessionSignsThePersonOutOfTheProvider()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        // The site signs out long after its ID token expired: the hint still names the client.
        api.Time.Advance(TimeSpan.FromHours(2));

        using var response = await _browser.GetAsync(EndSessionUrl(_siteHome, tokens.GetProperty("id_token").GetString()), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(new Uri(_siteHome + "?state=bye"), response.Headers.Location);
        var again = await AuthorizeAsync(_browser, AuthorizeUrl(Pkce.Create(), prompt: "none"));
        Assert.Equal("login_required", again["error"]);
    }

    [Fact]
    public async Task EndSessionRefusesAForeignReturnAddressAndKeepsTheSession()
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);

        using var response = await _browser.GetAsync(EndSessionUrl("https://evil.example.org/", null), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var again = await AuthorizeAsync(_browser, AuthorizeUrl(Pkce.Create(), prompt: "none"));
        Assert.True(again.ContainsKey("code"));
    }

    private static string EndSessionUrl(string returnTo, string? idToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = "web",
            ["post_logout_redirect_uri"] = returnTo,
            ["id_token_hint"] = idToken,
            ["state"] = "bye",
        };
        return IdentityModule.EndSessionPath + "?" + string.Join('&', query
            .Where(p => p.Value is not null)
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));
    }

    private static string Access(JsonElement tokens) => tokens.GetProperty("access_token").GetString()!;

    private static string Refresh(JsonElement tokens) => tokens.GetProperty("refresh_token").GetString()!;
}
