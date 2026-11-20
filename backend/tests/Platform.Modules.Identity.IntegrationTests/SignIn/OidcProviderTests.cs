using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Platform.Modules.Identity.SignIn;
using Platform.Testing.Containers;
using static Platform.Modules.Identity.IntegrationTests.SignIn.OidcFlow;

[assembly: AssemblyFixture(typeof(RedisFixture))]

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>
/// The OpenID Connect provider (E18-02, FR-ID-2, ADR-0018) on a real database: discovery, the authorization code flow
/// with PKCE, the API accepting its tokens, and the ways a request must fail — unknown client, foreign redirect
/// address, no or weak PKCE, wrong verifier, replayed, expired or foreign code, expired or forged access token.
/// </summary>
public sealed class OidcProviderTests(IdentityApi api) : IClassFixture<IdentityApi>, IAsyncLifetime
{
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
    public async Task DiscoveryDocumentDescribesTheProvider()
    {
        using var response = await _browser.GetAsync(IdentityModule.DiscoveryPath, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var discovery = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        Assert.Equal("http://localhost/", discovery.GetProperty("issuer").GetString());
        Assert.Equal("http://localhost/connect/authorize", discovery.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("http://localhost/connect/token", discovery.GetProperty("token_endpoint").GetString());
        Assert.Equal("http://localhost/.well-known/jwks", discovery.GetProperty("jwks_uri").GetString());
        Assert.Equal(["S256"], Strings(discovery, "code_challenge_methods_supported"));
        Assert.Equal(["code"], Strings(discovery, "response_types_supported"));
        Assert.Equal(["authorization_code"], Strings(discovery, "grant_types_supported"));
        Assert.Contains("openid", Strings(discovery, "scopes_supported"));
    }

    [Fact]
    public async Task KeySetPublishesOnlyThePublicSigningKey()
    {
        using var response = await _browser.GetAsync(IdentityModule.JwksPath, Ct);

        var keys = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("keys");
        var key = Assert.Single(keys.EnumerateArray());
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.False(key.TryGetProperty("d", out _), "the private exponent must never leave the provider");
        Assert.False(key.TryGetProperty("p", out _));
    }

    [Fact]
    public async Task SignedInPersonGetsTokensWhoseSubjectIsTheAccountId()
    {
        var (account, tokens) = await SignInAsync(api, _browser);

        Assert.Equal("Bearer", tokens.GetProperty("token_type").GetString());
        var (header, access) = Decode(tokens.GetProperty("access_token").GetString()!);
        Assert.Equal("at+jwt", header.GetProperty("typ").GetString());
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal(account.Id.ToString("D"), access.GetProperty("sub").GetString());
        Assert.Equal("http://localhost/", access.GetProperty("iss").GetString());
        Assert.Equal(IdentitySettings.DefaultAudience, Audience(access));
        // A token says who, never mail or a name (FR-ID-1).
        Assert.False(access.TryGetProperty("email", out _));

        var (_, id) = Decode(tokens.GetProperty("id_token").GetString()!);
        Assert.Equal(account.Id.ToString("D"), id.GetProperty("sub").GetString());
        Assert.Equal("web", Audience(id));
        Assert.Equal("nonce-1", id.GetProperty("nonce").GetString());
    }

    [Fact]
    public async Task ApiSeesTheAccountOfTheAccessToken()
    {
        var (account, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();

        using var signedIn = await WhoAmIAsync(client, tokens.GetProperty("access_token").GetString());
        using var anonymous = await WhoAmIAsync(client, null);

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(account.Id.ToString("D"), await signedIn.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task WithoutASessionThePersonGoesToTheSignInWindowAndBack()
    {
        var url = AuthorizeUrl(Pkce.Create());

        using var response = await _browser.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal(IdentitySettings.DefaultSignInPath, location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0]);
        Assert.Equal(url, Query(location.IsAbsoluteUri ? location : new Uri("http://localhost" + location.OriginalString))["ReturnUrl"]);
    }

    [Fact]
    public async Task PromptNoneWithoutASessionAnswersLoginRequired()
    {
        var result = await AuthorizeAsync(_browser, AuthorizeUrl(Pkce.Create(), prompt: "none"));

        Assert.Equal("login_required", result["error"]);
        Assert.False(result.ContainsKey("code"));
    }

    [Fact]
    public async Task SessionOfADeletedAccountGivesNoCode()
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);
        await DeleteAccountAsync(account.Id);

        var result = await AuthorizeAsync(_browser, AuthorizeUrl(Pkce.Create(), prompt: "none"));

        Assert.Equal("login_required", result["error"]);
    }

    [Theory]
    [InlineData("evil", IdentityApi.WebRedirect)]
    [InlineData("web", "https://evil.example.org/auth/callback")]
    [InlineData("web", "http://localhost:3000/auth/callback/../steal")]
    [InlineData("web", IdentityApi.AdminRedirect)]
    public async Task UnknownClientOrForeignRedirectIsRefusedWithoutRedirecting(string clientId, string redirectUri)
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);

        using var response = await _browser.GetAsync(AuthorizeUrl(Pkce.Create(), clientId, redirectUri), Ct);

        // No redirect at all: a code or an error sent to an unverified address would leak to it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData(false, "S256")]
    [InlineData(true, "plain")]
    public async Task AuthorizationWithoutStrongPkceGivesNoCode(bool withPkce, string method)
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);

        using var response = await _browser.GetAsync(AuthorizeUrl(withPkce ? Pkce.Create() : null, method: method), Ct);

        // Refused on the spot: no code, not even a redirect.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("invalid_request", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeIsWorthlessWithoutItsVerifier()
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);
        var code = await CodeAsync(_browser, Pkce.Create());

        var (status, body) = await ExchangeAsync(_browser, code, Pkce.Create().Verifier);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task CodeIsRedeemedOnlyOnce()
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);
        var pkce = Pkce.Create();
        var code = await CodeAsync(_browser, pkce);
        var (first, _) = await ExchangeAsync(_browser, code, pkce.Verifier);

        var (second, body) = await ExchangeAsync(_browser, code, pkce.Verifier);

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.BadRequest, second);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CodeOfOneClientIsRefusedToAnother()
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);
        var pkce = Pkce.Create();
        var code = await CodeAsync(_browser, pkce);

        var (status, body) = await ExchangeAsync(_browser, code, pkce.Verifier, "admin", IdentityApi.AdminRedirect);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ExpiredCodeIsRefused()
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);
        var pkce = Pkce.Create();
        var code = await CodeAsync(_browser, pkce);
        api.Time.Advance(TimeSpan.FromMinutes(3));

        var (status, body) = await ExchangeAsync(_browser, code, pkce.Verifier);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CodeOfAnAccountDeletedMeanwhileIsRefused()
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(_browser, account.Id);
        var pkce = Pkce.Create();
        var code = await CodeAsync(_browser, pkce);
        await DeleteAccountAsync(account.Id);

        var (status, body) = await ExchangeAsync(_browser, code, pkce.Verifier);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ExpiredAccessTokenIsRefused()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        using var client = api.CreateClient();
        api.Time.Advance(TimeSpan.FromMinutes(16));

        using var response = await WhoAmIAsync(client, tokens.GetProperty("access_token").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgedAccessTokensAreRefused()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        var genuine = tokens.GetProperty("access_token").GetString()!;
        var (_, claims) = Decode(genuine);
        using var client = api.CreateClient();

        // The same claims for another account, signed by a key the provider never had.
        var other = Guid.CreateVersion7(api.Time.GetUtcNow()).ToString("D");
        var forged = SignWithForeignKey(claims, other);
        // The genuine signature over a payload changed to another account.
        var parts = genuine.Split('.');
        var payload = JsonSerializer.Serialize(JsonSerializer.Deserialize<Dictionary<string, object>>(claims.GetRawText())!
            .Select(p => p.Key == "sub" ? new KeyValuePair<string, object>("sub", other) : p)
            .ToDictionary());
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";
        // An id token is no access token, though signed by the same key.
        var idToken = tokens.GetProperty("id_token").GetString()!;

        foreach (var token in new[] { forged, tampered, idToken, genuine + "x" })
        {
            using var response = await WhoAmIAsync(client, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private async Task DeleteAccountAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Accounts.Where(a => a.Id == id).ExecuteDeleteAsync(Ct);
    }

    private static string SignWithForeignKey(JsonElement claims, string subject)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(claims.GetRawText())!;
        values["sub"] = subject;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = values,
            TokenType = "at+jwt",
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "foreign" }, SecurityAlgorithms.RsaSha256),
        });
    }

    private static string[] Strings(JsonElement element, string name) =>
        [.. element.GetProperty(name).EnumerateArray().Select(e => e.GetString()!)];

    private static string? Audience(JsonElement claims) =>
        claims.GetProperty("aud") is { ValueKind: JsonValueKind.Array } array ? array[0].GetString() : claims.GetProperty("aud").GetString();
}
