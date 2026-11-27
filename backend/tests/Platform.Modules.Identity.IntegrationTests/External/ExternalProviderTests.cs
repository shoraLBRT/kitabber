using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Platform.Configuration;
using Platform.Modules.Identity.External;

namespace Platform.Modules.Identity.IntegrationTests.External;

/// <summary>
/// The real external providers (ADR-0115) without their servers: the address each sends the browser to, the code
/// exchange and the reading of the person against a stand-in HTTP handler, the Telegram Login signature, and the
/// settings that turn providers on.
/// </summary>
public sealed class ExternalProviderTests
{
    private const string _botToken = "123456:TEST-token_for-tests";

    private static readonly ExternalChallenge _challenge = new(
        "state-1", "verifier-verifier-verifier-verifier-verifier",
        new Uri("https://site.test/api/v1/sign-in/external/yandex/callback"), new Uri("https://site.test/"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void OAuthProvidersSendTheBrowserWithPkceAndTheirScope()
    {
        var http = new StubHttp();
        foreach (var (provider, host, scope) in new (IExternalSignInProvider, string, string)[]
                 {
                     (new YandexProvider(http, "yandex-id", "yandex-secret"), "oauth.yandex.ru", "login:email"),
                     (new VkProvider(http, "vk-id"), "id.vk.com", "email"),
                     (new GoogleProvider(http, "google-id", "google-secret"), "accounts.google.com", "openid email"),
                 })
        {
            var uri = provider.AuthorizationUri(_challenge);
            var query = SignIn.OidcFlow.Query(uri);
            Assert.Equal(host, uri.Host);
            Assert.Equal("code", query["response_type"]);
            Assert.Equal(scope, query["scope"]);
            Assert.Equal("state-1", query["state"]);
            Assert.Equal("S256", query["code_challenge_method"]);
            Assert.Equal(OAuthCodeProvider.CodeChallenge(_challenge.CodeVerifier), query["code_challenge"]);
            Assert.Equal(_challenge.Callback.AbsoluteUri, query["redirect_uri"]);
            Assert.True(provider.ReturnsState);
        }
    }

    [Fact]
    public async Task YandexExchangesTheCodeWithTheVerifierAndReadsTheDefaultEmail()
    {
        var http = new StubHttp();
        http.Answer("oauth.yandex.ru/token", """{"access_token":"at-1","token_type":"bearer"}""");
        http.Answer("login.yandex.ru/info", """{"id":"1130000","default_email":"reader@yandex.ru"}""");

        var profile = await new YandexProvider(http, "yandex-id", "yandex-secret")
            .CompleteAsync(new Dictionary<string, string> { ["code"] = "code-1" }, _challenge, Ct);

        Assert.Equal(new ExternalProfile("1130000", "reader@yandex.ru", true), profile);
        var token = http.Requests[0];
        Assert.Contains("code=code-1", token.Body, StringComparison.Ordinal);
        Assert.Contains("code_verifier=" + _challenge.CodeVerifier, token.Body, StringComparison.Ordinal);
        Assert.Contains("client_secret=yandex-secret", token.Body, StringComparison.Ordinal);
        Assert.Equal("OAuth at-1", http.Requests[1].Authorization);
    }

    [Fact]
    public async Task VkSendsTheDeviceAndReadsTheNumericUserId()
    {
        var http = new StubHttp();
        http.Answer("id.vk.com/oauth2/auth", """{"access_token":"at-2","user_id":42}""");
        http.Answer("id.vk.com/oauth2/user_info", """{"user":{"user_id":42,"email":"reader@vk.test"}}""");

        var profile = await new VkProvider(http, "vk-id").CompleteAsync(
            new Dictionary<string, string> { ["code"] = "code-2", ["device_id"] = "device-1" }, _challenge, Ct);

        Assert.Equal(new ExternalProfile("42", "reader@vk.test", true), profile);
        Assert.Contains("device_id=device-1", http.Requests[0].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret", http.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoogleCountsTheEmailOnlyWhenVerifiedAndAFailingServerProvesNobody()
    {
        var http = new StubHttp();
        http.Answer("oauth2.googleapis.com/token", """{"access_token":"at-3"}""");
        http.Answer("openidconnect.googleapis.com/v1/userinfo", """{"sub":"g-1","email":"r@gmail.com","email_verified":false}""");
        var google = new GoogleProvider(http, "google-id", "google-secret");
        var code = new Dictionary<string, string> { ["code"] = "code-3" };

        Assert.Equal(new ExternalProfile("g-1", "r@gmail.com", false), await google.CompleteAsync(code, _challenge, Ct));
        Assert.Equal("Bearer at-3", http.Requests[1].Authorization);

        var failing = new StubHttp();
        failing.Answer("oauth2.googleapis.com/token", """{"error":"invalid_grant"}""", HttpStatusCode.BadRequest);
        Assert.Null(await new GoogleProvider(failing, "google-id", "google-secret").CompleteAsync(code, _challenge, Ct));
        Assert.Null(await google.CompleteAsync(new Dictionary<string, string>(), _challenge, Ct));
    }

    [Fact]
    public async Task TelegramAcceptsOnlyFreshDataSignedForTheBot()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
        var telegram = new TelegramLoginProvider(_botToken, time);
        var data = Signed(time.GetUtcNow(), _botToken);

        Assert.Equal(new ExternalProfile("100500", null, false), await telegram.CompleteAsync(data, _challenge, Ct));
        Assert.False(telegram.ReturnsState);
        var uri = telegram.AuthorizationUri(_challenge);
        Assert.Equal("oauth.telegram.org", uri.Host);
        var query = SignIn.OidcFlow.Query(uri);
        Assert.Equal("123456", query["bot_id"]);
        Assert.Equal("https://site.test/auth/telegram", query["return_to"]);

        // Another bot's signature, a changed field, stale data.
        Assert.Null(await telegram.CompleteAsync(Signed(time.GetUtcNow(), "654321:another"), _challenge, Ct));
        Assert.Null(await telegram.CompleteAsync(new Dictionary<string, string>(data) { ["id"] = "1" }, _challenge, Ct));
        time.Advance(TelegramLoginProvider.MaxAge + TimeSpan.FromSeconds(1));
        Assert.Null(await telegram.CompleteAsync(data, _challenge, Ct));
        Assert.False(TelegramLoginProvider.Verify(new Dictionary<string, string> { ["hash"] = new string('z', 64), ["auth_date"] = "1" },
            _botToken, time.GetUtcNow()));
    }

    [Fact]
    public void ProvidersAreTurnedOnByTheirKeysAndTheFakeOnlyInTests()
    {
        Assert.Empty(Providers(new Dictionary<string, string?>(), "Production").Keys);
        Assert.Equal(["yandex", "vk", "google", "telegram"], Providers(new Dictionary<string, string?>
        {
            [ExternalSignInSettings.YandexClientIdKey] = "y",
            [ExternalSignInSettings.YandexClientSecretKey] = "ys",
            [ExternalSignInSettings.VkClientIdKey] = "v",
            [ExternalSignInSettings.GoogleClientIdKey] = "g",
            [ExternalSignInSettings.GoogleClientSecretKey] = "gs",
            [ExternalSignInSettings.TelegramBotTokenKey] = _botToken,
        }, "Production").Keys);
        Assert.Equal(["vk"], Providers(new Dictionary<string, string?> { [ExternalSignInSettings.VkClientIdKey] = "v" }, "Production").Keys);
        Assert.True(Providers(new Dictionary<string, string?> { [ExternalSignInSettings.FakeKey] = "true" }, "Test").IsFake);

        var refused = new[]
        {
            (new Dictionary<string, string?> { [ExternalSignInSettings.FakeKey] = "true" }, "Production"),
            (new Dictionary<string, string?> { [ExternalSignInSettings.GoogleClientIdKey] = "g" }, "Production"),
            (new Dictionary<string, string?> { [ExternalSignInSettings.TelegramBotTokenKey] = "not-a-token" }, "Production"),
        };
        foreach (var (settings, environment) in refused)
        {
            Assert.Throws<InvalidConfigurationException>(() => Providers(settings, environment));
        }
    }

    private static ExternalSignInProviders Providers(Dictionary<string, string?> settings, string environment)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddSingleton(TimeProvider.System);
        services.AddExternalSignIn(configuration, environment);
        return services.BuildServiceProvider().GetRequiredService<ExternalSignInProviders>();
    }

    private static Dictionary<string, string> Signed(DateTimeOffset at, string botToken)
    {
        var data = new Dictionary<string, string>
        {
            ["id"] = "100500",
            ["first_name"] = "Иван",
            ["username"] = "ivan",
            ["auth_date"] = at.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        data["hash"] = Convert.ToHexStringLower(TelegramLoginProvider.Sign(data, botToken));
        return data;
    }

    /// <summary>An HTTP client factory whose handler answers by the address and records each request.</summary>
    private sealed class StubHttp : IHttpClientFactory
    {
        private readonly Dictionary<string, (string Body, HttpStatusCode Status)> _answers = [];

        public List<(string Url, string Body, string? Authorization)> Requests { get; } = [];

        public void Answer(string address, string json, HttpStatusCode status = HttpStatusCode.OK) =>
            _answers[address] = (json, status);

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(StubHttp owner) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.Host + request.RequestUri.AbsolutePath;
                var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                owner.Requests.Add((url, body, request.Headers.Authorization?.ToString()));
                var (json, status) = owner._answers.TryGetValue(url, out var answer) ? answer : ("{}", HttpStatusCode.NotFound);
                return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }
        }
    }
}
