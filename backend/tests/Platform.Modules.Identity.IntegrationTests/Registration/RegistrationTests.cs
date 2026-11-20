using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.IntegrationTests.SignIn;
using Platform.Modules.Identity.Passwords;
using Platform.Modules.Identity.Registration;
using Platform.Modules.Identity.SignIn;
using static Platform.Modules.Identity.IntegrationTests.Registration.RegistrationApi;

namespace Platform.Modules.Identity.IntegrationTests.Registration;

/// <summary>
/// Registration by email and password (FR-AUTH, E03-01) through the API, with the letters delivered to Mailpit: the
/// code from the letter confirms the address and signs the person in through the usual authorization code flow; a
/// wrong or expired code is refused; a new code waits for the resend timer.
/// </summary>
public sealed class RegistrationTests(RegistrationApi api) : IClassFixture<RegistrationApi>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAndDeliverAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task NewPersonRegistersConfirmsWithTheCodeFromMailpitAndEndsUpSignedIn()
    {
        using var browser = api.CreateBrowser();
        var email = NewAddress("Ivan.Petrov");

        var (registration, code) = await api.RegisterAndReadCodeAsync(browser, email);
        using (var confirmed = await ConfirmAsync(browser, registration, code))
        {
            Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
            var cookie = Assert.Single(confirmed.Headers.GetValues("Set-Cookie"),
                c => c.StartsWith(IdentityModule.SessionCookieName + "=", StringComparison.Ordinal));
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        }

        // The provider's session now gives the site a code, and the code a token for this very account.
        var pkce = Pkce.Create();
        var authorizationCode = await OidcFlow.CodeAsync(browser, pkce);
        var (status, tokens) = await OidcFlow.ExchangeAsync(browser, authorizationCode, pkce.Verifier);
        Assert.Equal(HttpStatusCode.OK, status);
        using var whoAmI = await OidcFlow.WhoAmIAsync(browser, tokens.GetProperty("access_token").GetString());
        Assert.Equal(registration.ToString("D"), await whoAmI.Content.ReadAsStringAsync(Ct));

        var account = await StoredAsync(registration);
        Assert.Equal(email, account.Email);
        Assert.Equal(api.Time.GetUtcNow(), account.EmailConfirmedAt);
        Assert.False(account.IsPendingRegistration);
        Assert.StartsWith("$argon2id$v=19$", account.PasswordHash, StringComparison.Ordinal);
        Assert.True(PasswordHasher.Verify("secret-1", account.PasswordHash));
        Assert.False(PasswordHasher.Verify("secret-2", account.PasswordHash));
    }

    [Fact]
    public async Task WrongCodeIsRefusedAndSignsNobodyIn()
    {
        using var browser = api.CreateBrowser();
        var email = NewAddress();
        var (registration, code) = await api.RegisterAndReadCodeAsync(browser, email);
        var wrong = code == "000000" ? "111111" : "000000";

        using var refused = await ConfirmAsync(browser, registration, wrong);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(AccountEmailEndpoints.ConfirmationInvalidCode, await ProblemCodeAsync(refused));
        Assert.False(refused.Headers.Contains("Set-Cookie"));
        Assert.True((await StoredAsync(registration)).IsPendingRegistration);
        using var authorize = await browser.GetAsync(OidcFlow.AuthorizeUrl(Pkce.Create()), Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        Assert.Contains(IdentitySettings.DefaultSignInPath + "?", authorize.Headers.Location!.OriginalString, StringComparison.Ordinal);

        // The right code still works after one wrong try.
        using var confirmed = await ConfirmAsync(browser, registration, code);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
    }

    [Fact]
    public async Task ExpiredCodeIsRefused()
    {
        using var browser = api.CreateBrowser();
        var (registration, code) = await api.RegisterAndReadCodeAsync(browser, NewAddress());

        api.Time.Advance(EmailConfirmation.Lifetime);
        using var refused = await ConfirmAsync(browser, registration, code);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(AccountEmailEndpoints.ConfirmationInvalidCode, await ProblemCodeAsync(refused));
        Assert.Null((await StoredAsync(registration)).EmailConfirmedAt);
    }

    [Fact]
    public async Task NewCodeWaitsForTheTimerAndReplacesTheOldOne()
    {
        using var browser = api.CreateBrowser();
        var email = NewAddress();
        using var registered = await RegisterAsync(browser, email);
        var body = await registered.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(60, body.GetProperty("resendAfterSeconds").GetInt32());
        var registration = body.GetProperty("registrationId").GetGuid();
        var first = await api.CodeFromMailpitAsync(email);

        api.Time.Advance(TimeSpan.FromSeconds(45));
        using (var early = await ResendAsync(browser, registration))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, early.StatusCode);
            Assert.Equal(RegistrationEndpoints.CodeTooSoonCode, await ProblemCodeAsync(early));
            Assert.Equal(TimeSpan.FromSeconds(15), early.Headers.RetryAfter?.Delta);
        }

        api.Time.Advance(TimeSpan.FromSeconds(15));
        using (var resent = await ResendAsync(browser, registration))
        {
            Assert.Equal(HttpStatusCode.Accepted, resent.StatusCode);
            Assert.Equal(60, (await resent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("resendAfterSeconds").GetInt32());
        }

        var second = await api.CodeFromMailpitAsync(email, count: 2);
        if (second != first)
        {
            using var old = await ConfirmAsync(browser, registration, first);
            Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        }

        using var confirmed = await ConfirmAsync(browser, registration, second);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "secret-1", "email")]
    [InlineData(null, "secret-1", "email")]
    [InlineData("valid@example.com", "12345", "password")]
    [InlineData("valid@example.com", "      ", "password")]
    [InlineData("valid@example.com", null, "password")]
    public async Task InvalidEmailOrPasswordIsAValidationError(string? email, string? password, string field)
    {
        using var client = api.CreateClient();

        using var response = await RegisterAsync(client, email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
        Assert.False(await AnyAccountAsync());
    }

    [Fact]
    public async Task AddressOfAConfirmedAccountCannotBeRegistered()
    {
        using var browser = api.CreateBrowser();
        var email = NewAddress("Anna.Smith");
        var (registration, code) = await api.RegisterAndReadCodeAsync(browser, email);
        (await ConfirmAsync(browser, registration, code)).Dispose();

        using var client = api.CreateClient();
        using var again = await RegisterAsync(client, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(AccountEmailEndpoints.EmailTakenCode, await ProblemCodeAsync(again));
    }

    [Fact]
    public async Task UnconfirmedRegistrationDoesNotLockTheAddressAndCannotBeConfirmedByAStranger()
    {
        // A stranger registers someone's address with a password of their own and never sees the letter.
        using var stranger = api.CreateClient();
        var email = NewAddress("owner");
        var (strangers, _) = await api.RegisterAndReadCodeAsync(stranger, email);

        // While the resend timer runs, the address cannot be registered again: that would flood the mailbox.
        using var owner = api.CreateBrowser();
        using (var early = await RegisterAsync(owner, email, "owners-password"))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, early.StatusCode);
            Assert.Equal(RegistrationEndpoints.CodeTooSoonCode, await ProblemCodeAsync(early));
        }

        // Then the owner registers it: the stranger's registration is gone, and the code from the mailbox confirms only
        // the owner's.
        api.Time.Advance(EmailConfirmation.ResendInterval);
        using var registered = await RegisterAsync(owner, email, "owners-password");
        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);
        var owners = (await registered.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("registrationId").GetGuid();
        var code = await api.CodeFromMailpitAsync(email, count: 2);

        using (var forStranger = await ConfirmAsync(stranger, strangers, code))
        {
            Assert.Equal(HttpStatusCode.BadRequest, forStranger.StatusCode);
        }

        using var confirmed = await ConfirmAsync(owner, owners, code);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        var account = await StoredAsync(owners);
        Assert.True(PasswordHasher.Verify("owners-password", account.PasswordHash));
        await using var scope = api.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AnyAsync(a => a.Id == strangers, Ct));
    }

    [Fact]
    public async Task ConfirmedAccountIsNeverSignedInByItsIdAndACode()
    {
        using var browser = api.CreateBrowser();
        var (registration, code) = await api.RegisterAndReadCodeAsync(browser, NewAddress());
        (await ConfirmAsync(browser, registration, code)).Dispose();

        using var other = api.CreateClient();
        using var again = await ConfirmAsync(other, registration, code);
        using var resend = await ResendAsync(other, registration);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.False(again.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.NotFound, resend.StatusCode);
        Assert.Equal(RegistrationEndpoints.RegistrationNotFoundCode, await ProblemCodeAsync(resend));
    }

    [Fact]
    public async Task RegistrationsAreLimitedPerAddress()
    {
        using var client = api.CreateClient();
        for (var i = 0; i < IdentitySettings.RegistrationPolicy.PermitLimit; i++)
        {
            using var accepted = await RegisterAsync(client, "x", "secret-1");
            Assert.Equal(HttpStatusCode.BadRequest, accepted.StatusCode);
        }

        using var limited = await RegisterAsync(client, NewAddress());

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("too_many_requests", await ProblemCodeAsync(limited));
    }

    private async Task<Account> StoredAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking().SingleAsync(a => a.Id == id, Ct);
    }

    private async Task<bool> AnyAccountAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AnyAsync(Ct);
    }
}
