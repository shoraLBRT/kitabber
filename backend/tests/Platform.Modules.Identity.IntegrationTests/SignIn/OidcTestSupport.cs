using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Platform.Http;
using Platform.Modules.Identity.SignIn;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>
/// The Api host with the Identity module on PostgreSQL and Redis, both clients configured, and two test endpoints:
/// one that stands in for the sign-in window (E03, not built yet) by setting the provider's session cookie, and one
/// that says which account the API sees in a request.
/// </summary>
public class IdentityApi : IntegrationApiFactory
{
    public const string WebRedirect = "http://localhost:3000/auth/callback";
    public const string AdminRedirect = "http://localhost:5173/auth/callback";

    public IdentityApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
        WithSetting(IdentitySettings.WebRedirectUrisKey, WebRedirect);
        WithSetting(IdentitySettings.AdminRedirectUrisKey, AdminRedirect);
        WithModule(services => services.AddApiEndpoints<SignInTestEndpoints>());
    }

    /// <summary>Empties the database and Redis, then writes the clients again, as the API does at start.</summary>
    public async Task ResetWithClientsAsync()
    {
        await ResetAsync();
        await Services.GetRequiredService<OidcClientSync>().SyncAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A browser: keeps cookies, does not follow redirects, so a test sees every hop.</summary>
    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>A new account in the database.</summary>
    public async Task<Account> CreateAccountAsync()
    {
        var account = Account.Create(Time);
        await SaveAsync<IdentityDbContext>(account);
        return account;
    }
}

/// <summary>The test endpoints of <see cref="IdentityApi"/>.</summary>
public sealed class SignInTestEndpoints : IApiEndpoints
{
    public const string SessionPath = "/api/v1/test/identity-session/";
    public const string WhoAmIPath = "/api/v1/test/whoami";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        // The sign-in window's last step, once the person has proved who they are (#28): the provider's session.
        v1.MapPost("/test/identity-session/{accountId:guid}", async (Guid accountId, HttpContext context) =>
        {
            var identity = new ClaimsIdentity([new Claim("sub", accountId.ToString("D"))], IdentityModule.SessionScheme);
            await context.SignInAsync(IdentityModule.SessionScheme, new ClaimsPrincipal(identity));
            return Results.NoContent();
        });
        v1.MapGet("/test/whoami", (ClaimsPrincipal user) => Results.Text(user.FindFirst("sub")?.Value ?? string.Empty))
            .RequireAuthorization();
    }
}

/// <summary>A PKCE pair (RFC 7636) with the S256 method.</summary>
public sealed record Pkce(string Verifier, string Challenge)
{
    public static Pkce Create()
    {
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new Pkce(verifier, Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }
}

/// <summary>The steps of the authorization code flow, as a client makes them.</summary>
public static class OidcFlow
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string AuthorizeUrl(
        Pkce? pkce,
        string clientId = "web",
        string redirectUri = IdentityApi.WebRedirect,
        string method = "S256",
        string? prompt = null,
        string state = "state-1",
        string nonce = "nonce-1",
        string scope = "openid offline_access")
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["prompt"] = prompt,
        };
        if (pkce is not null)
        {
            query["code_challenge"] = method == "plain" ? pkce.Verifier : pkce.Challenge;
            query["code_challenge_method"] = method;
        }

        return IdentityModule.AuthorizationPath + "?" + string.Join('&', query
            .Where(p => p.Value is not null)
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));
    }

    public static async Task SignInAtProviderAsync(HttpClient browser, Guid accountId)
    {
        using var response = await browser.PostAsync(SignInTestEndpoints.SessionPath + accountId, null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>The query parameters of the redirect back to the client.</summary>
    public static async Task<Dictionary<string, string>> AuthorizeAsync(HttpClient browser, string url)
    {
        using var response = await browser.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return Query(response.Headers.Location!);
    }

    public static async Task<string> CodeAsync(HttpClient browser, Pkce pkce, string clientId = "web")
    {
        var redirect = clientId == "web" ? IdentityApi.WebRedirect : IdentityApi.AdminRedirect;
        var result = await AuthorizeAsync(browser, AuthorizeUrl(pkce, clientId, redirect));
        Assert.Equal("state-1", result["state"]);
        return result["code"];
    }

    public static async Task<(HttpStatusCode Status, JsonElement Body)> ExchangeAsync(
        HttpClient client, string code, string verifier, string clientId = "web", string redirectUri = IdentityApi.WebRedirect)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
        });
        using var response = await client.PostAsync(IdentityModule.TokenPath, form, Ct);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
        return (response.StatusCode, body);
    }

    /// <summary>Signs a new account in and runs the whole flow: the account and the token response.</summary>
    public static async Task<(Account Account, JsonElement Tokens)> SignInAsync(IdentityApi api, HttpClient browser)
    {
        var account = await api.CreateAccountAsync();
        await SignInAtProviderAsync(browser, account.Id);
        var pkce = Pkce.Create();
        var code = await CodeAsync(browser, pkce);
        var (status, tokens) = await ExchangeAsync(browser, code, pkce.Verifier);
        Assert.Equal(HttpStatusCode.OK, status);
        return (account, tokens);
    }

    /// <summary>A refresh token grant (ADR-0024), as the site's server makes it.</summary>
    public static Task<(HttpStatusCode Status, JsonElement Body)> RefreshAsync(HttpClient client, string refreshToken, string clientId = "web") =>
        PostFormAsync(client, IdentityModule.TokenPath, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
        });

    /// <summary>The revocation of a token (RFC 7009) by the client it was issued to.</summary>
    public static async Task<HttpStatusCode> RevokeAsync(HttpClient client, string token, string clientId = "web")
    {
        var (status, _) = await PostFormAsync(client, IdentityModule.RevocationPath, new Dictionary<string, string>
        {
            ["token"] = token,
            ["token_type_hint"] = "refresh_token",
            ["client_id"] = clientId,
        });
        return status;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> PostFormAsync(
        HttpClient client, string path, Dictionary<string, string> fields)
    {
        using var form = new FormUrlEncodedContent(fields);
        using var response = await client.PostAsync(path, form, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        var body = JsonDocument.Parse(text.Length == 0 ? "{}" : text).RootElement.Clone();
        return (response.StatusCode, body);
    }

    public static async Task<HttpResponseMessage> WhoAmIAsync(HttpClient client, string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SignInTestEndpoints.WhoAmIPath);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }

        return await client.SendAsync(request, Ct);
    }

    public static Dictionary<string, string> Query(Uri uri) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : string.Empty));

    /// <summary>The header and the claims of a JWT, without checking it.</summary>
    public static (JsonElement Header, JsonElement Payload) Decode(string jwt)
    {
        var parts = jwt.Split('.');
        return (Parse(parts[0]), Parse(parts[1]));
        static JsonElement Parse(string part) => JsonDocument.Parse(Base64UrlEncoder.Decode(part)).RootElement.Clone();
    }
}
