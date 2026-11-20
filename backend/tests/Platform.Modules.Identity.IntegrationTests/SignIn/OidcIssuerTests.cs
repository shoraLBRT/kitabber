using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Platform.Http;
using Platform.Modules.Identity.SignIn;
using Platform.Testing.Api;
using Platform.Testing.Containers;
using static Platform.Modules.Identity.IntegrationTests.SignIn.OidcFlow;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>The Identity host that announces a public issuer address of its own (Identity:Issuer).</summary>
public sealed class PublicIssuerIdentityApi : IdentityApi
{
    public const string Issuer = "https://accounts.example.org/";

    public PublicIssuerIdentityApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithSetting(IdentitySettings.IssuerKey, Issuer);
}

/// <summary>
/// Moving the issuer to another address is a change of settings only (FR-ID-2, ADR-0002): the provider announces and
/// signs with the configured address, and the API trusts whichever provider Auth:Authority names.
/// </summary>
public sealed class OidcIssuerTests(PublicIssuerIdentityApi api) : IClassFixture<PublicIssuerIdentityApi>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetWithClientsAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ProviderAnnouncesAndSignsWithTheConfiguredIssuer()
    {
        using var browser = api.CreateBrowser();
        using var response = await browser.GetAsync(IdentityModule.DiscoveryPath, Ct);
        var discovery = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;

        var (account, tokens) = await SignInAsync(api, browser);
        using var client = api.CreateClient();
        using var whoAmI = await WhoAmIAsync(client, tokens.GetProperty("access_token").GetString());

        Assert.Equal(PublicIssuerIdentityApi.Issuer, discovery.GetProperty("issuer").GetString());
        Assert.Equal(PublicIssuerIdentityApi.Issuer, Decode(tokens.GetProperty("access_token").GetString()!).Payload.GetProperty("iss").GetString());
        Assert.Equal(PublicIssuerIdentityApi.Issuer, Decode(tokens.GetProperty("id_token").GetString()!).Payload.GetProperty("iss").GetString());
        Assert.Equal(account.Id.ToString("D"), await whoAmI.Content.ReadAsStringAsync(Ct));
    }
}

/// <summary>
/// The API switched to an external provider by <c>Auth:Authority</c> alone. The provider is a fake (docs/TESTING.md):
/// discovery and keys answered in process, tokens minted by the test. No database: the API only validates.
/// </summary>
public sealed class ExternalAuthorityTests : IAsyncLifetime
{
    private const string _authority = "https://external.example.org/";
    private readonly FakeExternalProvider _provider = new(_authority);
    private readonly PlatformApiFactory _api;

    public ExternalAuthorityTests() =>
        _api = new PlatformApiFactory()
            .WithSetting(IdentitySettings.AuthorityKey, _authority)
            .WithModule(services =>
            {
                services.AddApiEndpoints<SignInTestEndpoints>();
                services.AddSingleton<IHttpMessageHandlerBuilderFilter>(_provider);
            });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
        _provider.Dispose();
    }

    [Fact]
    public async Task ApiAcceptsTheExternalProvidersTokens()
    {
        var account = Guid.CreateVersion7(_api.Time.GetUtcNow());
        using var client = _api.CreateClient();

        using var response = await WhoAmIAsync(client, _provider.AccessToken(account, _api.Time.GetUtcNow()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(account.ToString("D"), await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task ApiRefusesTokensOfAnyOtherIssuerAudienceOrKey()
    {
        var account = Guid.CreateVersion7(_api.Time.GetUtcNow());
        var now = _api.Time.GetUtcNow();
        using var client = _api.CreateClient();

        foreach (var token in new[]
                 {
                     _provider.AccessToken(account, now, issuer: "https://other.example.org/"),
                     _provider.AccessToken(account, now, audience: "another-api"),
                     _provider.AccessToken(account, now, foreignKey: true),
                     _provider.AccessToken(account, now - TimeSpan.FromHours(1)),
                 })
        {
            using var response = await WhoAmIAsync(client, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}

/// <summary>
/// An external OpenID Connect provider for the API's validation: answers discovery and the key set over the API's
/// HTTP clients and mints access tokens with its key.
/// </summary>
internal sealed class FakeExternalProvider(string issuer) : IHttpMessageHandlerBuilderFilter, IDisposable
{
    private static readonly string[] _code = ["code"];
    private static readonly string[] _public = ["public"];
    private static readonly string[] _rs256 = ["RS256"];

    private readonly RSA _key = RSA.Create(2048);
    private readonly RSA _foreign = RSA.Create(2048);

    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        builder.PrimaryHandler = new Handler(this);
    };

    public string AccessToken(
        Guid account, DateTimeOffset issuedAt, string? issuer = null, string? audience = null, bool foreignKey = false)
    {
        var key = new RsaSecurityKey(foreignKey ? _foreign : _key) { KeyId = foreignKey ? "foreign" : "key-1" };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience ?? IdentitySettings.DefaultAudience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = issuedAt.UtcDateTime.AddMinutes(15),
            TokenType = "at+jwt",
            Claims = new Dictionary<string, object> { ["sub"] = account.ToString("D"), ["jti"] = Guid.NewGuid().ToString("N") },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
    }

    private string Issuer { get; } = issuer;

    public void Dispose()
    {
        _key.Dispose();
        _foreign.Dispose();
    }

    private string Respond(Uri uri)
    {
        if (uri.AbsolutePath == "/.well-known/openid-configuration")
        {
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["issuer"] = Issuer,
                ["jwks_uri"] = Issuer + ".well-known/jwks",
                ["authorization_endpoint"] = Issuer + "connect/authorize",
                ["token_endpoint"] = Issuer + "connect/token",
                ["response_types_supported"] = _code,
                ["subject_types_supported"] = _public,
                ["id_token_signing_alg_values_supported"] = _rs256,
            });
        }

        var parameters = _key.ExportParameters(false);
        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new Dictionary<string, string>
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["alg"] = "RS256",
                    ["kid"] = "key-1",
                    ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
                    ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!),
                },
            },
        });
    }

    private sealed class Handler(FakeExternalProvider provider) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var known = uri.GetLeftPart(UriPartial.Authority) + "/" == provider.Issuer
                && uri.AbsolutePath is "/.well-known/openid-configuration" or "/.well-known/jwks";
            return Task.FromResult(known
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(provider.Respond(uri), Encoding.UTF8, "application/json"),
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
