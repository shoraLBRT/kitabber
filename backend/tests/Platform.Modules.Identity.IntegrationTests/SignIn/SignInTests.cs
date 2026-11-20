using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.IntegrationTests.Registration;
using Platform.Modules.Identity.Registration;
using Platform.Modules.Identity.SignIn;
using static Platform.Modules.Identity.IntegrationTests.Registration.RegistrationApi;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>
/// Signing in by email (FR-AUTH, E03-02, ADR-0022) through the API, with the letters delivered to Mailpit: with the
/// password, or with a one-time code from a letter. Both end in the provider's session and the usual authorization code
/// flow; neither tells whether an address is registered.
/// </summary>
public sealed class SignInTests(RegistrationApi api) : IClassFixture<RegistrationApi>, IAsyncLifetime
{
    private const string Password = "secret-1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAndDeliverAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RegisteredPersonSignsInWithThePasswordAndGetsTokensForTheirAccount()
    {
        var (email, account) = await RegisteredAsync("Ivan.Petrov");

        using var browser = api.CreateBrowser();
        using (var signedIn = await PasswordAsync(browser, email.ToUpperInvariant(), Password))
        {
            Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
            var cookie = Assert.Single(signedIn.Headers.GetValues("Set-Cookie"),
                c => c.StartsWith(IdentityModule.SessionCookieName + "=", StringComparison.Ordinal));
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(account, await WhoAmIAfterTheCodeFlowAsync(browser));
    }

    [Fact]
    public async Task WrongPasswordUnknownAddressAndUnconfirmedRegistrationAreAnsweredAlike()
    {
        var (email, _) = await RegisteredAsync();
        using var pending = api.CreateClient();
        var unconfirmed = NewAddress("pending");
        (await RegisterAsync(pending, unconfirmed, Password)).Dispose();

        using var browser = api.CreateBrowser();
        var answers = new List<string>();
        foreach (var (address, password) in new[]
                 {
                     (email, "wrong-password"), (NewAddress("nobody"), Password), (unconfirmed, Password),
                     ("not-an-email", Password), (email, "12345"), (email, (string?)null),
                 })
        {
            using var refused = await PasswordAsync(browser, address, password);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.False(refused.Headers.Contains("Set-Cookie"));
            var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(Ct);
            answers.Add(string.Join('|', problem.GetProperty("status"), problem.GetProperty("code"),
                problem.GetProperty("title"), problem.GetProperty("detail")));
        }

        Assert.Equal(SignInEndpoints.InvalidCredentialsCode, answers[0].Split('|')[1]);
        Assert.Single(answers.Distinct());
        using var authorize = await browser.GetAsync(OidcFlow.AuthorizeUrl(Pkce.Create()), Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        Assert.Contains(IdentitySettings.DefaultSignInPath + "?", authorize.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeFromTheLetterSignsInTheAccountWithThatAddress()
    {
        var (email, account) = await RegisteredAsync("Anna.Smith");

        using var browser = api.CreateBrowser();
        var (challenge, code) = await RequestAndReadCodeAsync(browser, email, letter: 2);
        using (var signedIn = await ConfirmCodeAsync(browser, challenge, code))
        {
            Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
        }

        Assert.Equal(account, await WhoAmIAfterTheCodeFlowAsync(browser));
        using var mailpit = api.Mailpit.CreateClient();
        var letter = await mailpit.GetAsync((await mailpit.SearchAsync(email, Ct))[0].Id, Ct);
        Assert.Contains("Код для входа", letter.Subject, StringComparison.Ordinal);
        Assert.Contains("10 минут", letter.Text, StringComparison.Ordinal);

        // A code signs in once.
        using var again = await ConfirmCodeAsync(api.CreateBrowser(), challenge, code);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task CodeRequestIsAnsweredAlikeForAnyAddressAndCreatesTheAccountOfANewOne()
    {
        var (registered, _) = await RegisteredAsync();
        var unknown = NewAddress("Newcomer");

        using var browser = api.CreateBrowser();
        var answers = new List<string>();
        foreach (var address in new[] { registered, unknown })
        {
            using var sent = await RequestCodeAsync(browser, address);
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
            var body = await sent.Content.ReadFromJsonAsync<JsonElement>(Ct);
            answers.Add(string.Join(',', body.EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}")));
            Assert.Equal(60, body.GetProperty("resendAfterSeconds").GetInt32());

            using var early = await RequestCodeAsync(browser, address);
            Assert.Equal(HttpStatusCode.TooManyRequests, early.StatusCode);
            Assert.Equal(RegistrationEndpoints.CodeTooSoonCode, await ProblemCodeAsync(early));
        }

        Assert.Single(answers.Distinct());

        // The code of the new address creates an account with that address confirmed and no password.
        api.Time.Advance(SignInCode.ResendInterval);
        var (challenge, code) = await RequestAndReadCodeAsync(browser, unknown, letter: 2);
        (await ConfirmCodeAsync(browser, challenge, code)).Dispose();
        var created = await WhoAmIAfterTheCodeFlowAsync(browser);
        var account = await StoredAsync(created);
        Assert.Equal(unknown, account.Email);
        Assert.Equal(api.Time.GetUtcNow(), account.EmailConfirmedAt);
        Assert.Null(account.PasswordHash);
    }

    [Fact]
    public async Task CodeTakesTheAddressFromARegistrationThatWasNeverConfirmed()
    {
        // A stranger registers the owner's address with a password of their own and never sees the letter.
        var email = NewAddress("owner");
        using var stranger = api.CreateClient();
        using var registered = await RegisterAsync(stranger, email, "strangers-1");
        var strangers = (await registered.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("registrationId").GetGuid();

        // The owner signs in with a code from the mailbox: the address is theirs, the registration is gone.
        using var owner = api.CreateBrowser();
        var (challenge, code) = await RequestAndReadCodeAsync(owner, email, letter: 2);
        using (var signedIn = await ConfirmCodeAsync(owner, challenge, code))
        {
            Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
        }

        var created = await WhoAmIAfterTheCodeFlowAsync(owner);
        Assert.NotEqual(strangers, created);
        Assert.Null(await FindAsync(strangers));
        Assert.Equal(email, (await StoredAsync(created)).Email);
        using var strangersPassword = await PasswordAsync(api.CreateBrowser(), email, "strangers-1");
        Assert.Equal(HttpStatusCode.BadRequest, strangersPassword.StatusCode);
    }

    [Fact]
    public async Task AccountThatTypedTheAddressLosesItToTheOwnerWhoSignsInWithACode()
    {
        var email = NewAddress("typed");
        var other = await api.CreateAccountAsync();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var stored = await db.Accounts.SingleAsync(a => a.Id == other.Id, Ct);
            stored.SetUnconfirmedEmail(email, EmailAddresses.Normalize(email)!);
            await db.SaveChangesAsync(Ct);
        }

        using var owner = api.CreateBrowser();
        var (challenge, code) = await RequestAndReadCodeAsync(owner, email);
        (await ConfirmCodeAsync(owner, challenge, code)).Dispose();

        var created = await WhoAmIAfterTheCodeFlowAsync(owner);
        Assert.NotEqual(other.Id, created);
        Assert.Null((await StoredAsync(other.Id)).NormalizedEmail);
        Assert.NotNull((await StoredAsync(created)).EmailConfirmedAt);
    }

    [Fact]
    public async Task WrongExpiredAndSpentCodesAreRefused()
    {
        using var browser = api.CreateBrowser();
        var email = NewAddress();
        var (challenge, code) = await RequestAndReadCodeAsync(browser, email);
        var wrong = code == "000000" ? "111111" : "000000";

        // A code of another request, or with no request, signs nobody in.
        using (var unknown = await ConfirmCodeAsync(browser, Guid.NewGuid(), code))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
            Assert.Equal(AccountEmailEndpoints.ConfirmationInvalidCode, await ProblemCodeAsync(unknown));
        }

        // Five wrong tries spend the code.
        for (var i = 0; i < SignInCode.MaxAttempts; i++)
        {
            using var refused = await ConfirmCodeAsync(browser, challenge, wrong);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.False(refused.Headers.Contains("Set-Cookie"));
        }

        using (var spent = await ConfirmCodeAsync(browser, challenge, code))
        {
            Assert.Equal(HttpStatusCode.BadRequest, spent.StatusCode);
        }

        // A new code expires after ten minutes.
        api.Time.Advance(SignInCode.ResendInterval);
        var (second, secondCode) = await RequestAndReadCodeAsync(browser, email, letter: 2);
        api.Time.Advance(SignInCode.Lifetime);
        using var expired = await ConfirmCodeAsync(browser, second, secondCode);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        Assert.False(await AnyAccountAsync());
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CodeRequestForWhatIsNotAnAddressIsAValidationError(string? email)
    {
        using var client = api.CreateClient();

        using var response = await RequestCodeAsync(client, email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task PasswordAttemptsAreLimitedPerEmailAddress()
    {
        var (email, _) = await RegisteredAsync();
        for (var i = 0; i < IdentitySettings.SignInAddressPolicy.PermitLimit; i++)
        {
            // The case of the address gives no more tries: the counter is the normalised address.
            using var client = api.CreateClient();
            using var refused = await PasswordAsync(client, i % 2 == 0 ? email : email.ToUpperInvariant(), "wrong-password");
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        using var limited = await PasswordAsync(api.CreateBrowser(), email, Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("too_many_requests", await ProblemCodeAsync(limited));
        Assert.NotNull(limited.Headers.RetryAfter);

        // Another address is not affected, and the window passes.
        using (var other = await PasswordAsync(api.CreateBrowser(), NewAddress(), Password))
        {
            Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        }

        api.Time.Advance(IdentitySettings.SignInAddressPolicy.Window);
        using var later = await PasswordAsync(api.CreateBrowser(), email, Password);
        Assert.Equal(HttpStatusCode.NoContent, later.StatusCode);
    }

    [Fact]
    public async Task CodeLettersAreLimitedPerEmailAddress()
    {
        using var client = api.CreateClient();
        var email = NewAddress();
        for (var i = 0; i < IdentitySettings.SignInCodeAddressPolicy.PermitLimit; i++)
        {
            using var sent = await RequestCodeAsync(client, email);
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
            api.Time.Advance(SignInCode.ResendInterval);
        }

        using var limited = await RequestCodeAsync(client, email);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("too_many_requests", await ProblemCodeAsync(limited));
    }

    [Fact]
    public async Task CodeRequestsAreLimitedPerNetworkAddress()
    {
        using var client = api.CreateClient();
        for (var i = 0; i < IdentitySettings.SignInCodePolicy.PermitLimit; i++)
        {
            using var refused = await RequestCodeAsync(client, "x");
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        using var limited = await RequestCodeAsync(client, NewAddress());

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("too_many_requests", await ProblemCodeAsync(limited));
    }

    private static Task<HttpResponseMessage> PasswordAsync(HttpClient client, string? email, string? password) =>
        client.PostAsJsonAsync("/api/v1/sign-in/password", new { email, password }, Ct);

    private static Task<HttpResponseMessage> RequestCodeAsync(HttpClient client, string? email) =>
        client.PostAsJsonAsync("/api/v1/sign-in/code", new { email }, Ct);

    private static Task<HttpResponseMessage> ConfirmCodeAsync(HttpClient client, Guid challengeId, string code) =>
        client.PostAsJsonAsync($"/api/v1/sign-in/code/{challengeId}/confirmation", new { code }, Ct);

    private async Task<(Guid ChallengeId, string Code)> RequestAndReadCodeAsync(HttpClient client, string email, int letter = 1)
    {
        using var sent = await RequestCodeAsync(client, email);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var challenge = (await sent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("challengeId").GetGuid();
        return (challenge, await api.CodeFromMailpitAsync(email, letter));
    }

    // A person who registered with Password and confirmed the address: the address and the account.
    private async Task<(string Email, Guid Account)> RegisteredAsync(string name = "reader")
    {
        using var client = api.CreateClient();
        var email = NewAddress(name);
        var (registration, code) = await api.RegisterAndReadCodeAsync(client, email);
        using var confirmed = await ConfirmAsync(client, registration, code);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        return (email, registration);
    }

    // The account the provider's session belongs to, as the site would learn it: code, token, then the API.
    private static async Task<Guid> WhoAmIAfterTheCodeFlowAsync(HttpClient browser)
    {
        var pkce = Pkce.Create();
        var authorizationCode = await OidcFlow.CodeAsync(browser, pkce);
        var (status, tokens) = await OidcFlow.ExchangeAsync(browser, authorizationCode, pkce.Verifier);
        Assert.Equal(HttpStatusCode.OK, status);
        using var whoAmI = await OidcFlow.WhoAmIAsync(browser, tokens.GetProperty("access_token").GetString());
        return Guid.Parse(await whoAmI.Content.ReadAsStringAsync(Ct));
    }

    private async Task<Account?> FindAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id, Ct);
    }

    private async Task<Account> StoredAsync(Guid id) => await FindAsync(id) ?? throw new InvalidOperationException($"no account {id}");

    private async Task<bool> AnyAccountAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AnyAsync(Ct);
    }
}
