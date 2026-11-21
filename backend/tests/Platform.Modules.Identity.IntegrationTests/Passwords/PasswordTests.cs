using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.IntegrationTests.Registration;
using Platform.Modules.Identity.IntegrationTests.SignIn;
using Platform.Modules.Identity.Passwords;
using Platform.Modules.Identity.Registration;
using Platform.Modules.Identity.SignIn;
using Platform.Testing.Containers;
using static Platform.Modules.Identity.IntegrationTests.Registration.RegistrationApi;

namespace Platform.Modules.Identity.IntegrationTests.Passwords;

/// <summary>
/// The password after registration (FR-AUTH, E03-05, ADR-0036) through the API, with the letters delivered to Mailpit:
/// a reset by a one-time code from a letter, which ends every earlier session, and a change in the settings with the
/// current password. Both tell the owner by a letter.
/// </summary>
public sealed class PasswordTests(RegistrationApi api) : IClassFixture<RegistrationApi>, IAsyncLifetime
{
    private const string _password = "secret-1";
    private const string _newPassword = "brand-new-2";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAndDeliverAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ResetEndsEveryEarlierSessionAndTheNewPasswordSignsIn()
    {
        var (email, account) = await RegisteredAsync("Olga.Reset");
        // A session from before the reset: the provider's cookie and the site's refresh token.
        using var before = api.CreateBrowser();
        (await PasswordSignInAsync(before, email, _password)).Dispose();
        var tokens = await TokensAfterTheCodeFlowAsync(before);

        using var browser = api.CreateBrowser();
        var (resetId, code) = await RequestAndReadCodeAsync(browser, email, letter: 2);
        using (var reset = await ConfirmAsync(browser, resetId, code, _newPassword))
        {
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
            Assert.Contains(reset.Headers.GetValues("Set-Cookie"),
                c => c.StartsWith(IdentityModule.SessionCookieName + "=", StringComparison.Ordinal));
        }

        // The person is signed in at once with the new session.
        Assert.Equal(account, await WhoAmIAsync(await TokensAfterTheCodeFlowAsync(browser)));

        // The provider's session from before is over: authorize sends the person to sign in.
        using (var authorize = await before.GetAsync(OidcFlow.AuthorizeUrl(Pkce.Create()), Ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
            Assert.Contains(IdentitySettings.DefaultSignInPath + "?", authorize.Headers.Location!.OriginalString, StringComparison.Ordinal);
        }

        // So is the site's refresh token from before.
        var (status, body) = await OidcFlow.RefreshAsync(before, tokens.GetProperty("refresh_token").GetString()!);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());

        // The old password no longer signs in, the new one does.
        using (var old = await PasswordSignInAsync(api.CreateBrowser(), email, _password))
        {
            Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        }

        using var later = api.CreateBrowser();
        using (var signedIn = await PasswordSignInAsync(later, email, _newPassword))
        {
            Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
        }

        Assert.Equal(account, await WhoAmIAsync(await TokensAfterTheCodeFlowAsync(later)));

        // The letters: the code, then the notice of the change.
        var letters = await LettersAsync(email, 3);
        Assert.Equal("Пароль изменён", letters[0].Subject);
        Assert.StartsWith("Код для восстановления пароля", letters[1].Subject, StringComparison.Ordinal);
        using var mailpit = api.Mailpit.CreateClient();
        var codeLetter = await mailpit.GetAsync(letters[1].Id, Ct);
        Assert.Contains(PasswordReset.LifetimeText, codeLetter.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeWorksOnceAndOnlyForItsLifetime()
    {
        var (email, _) = await RegisteredAsync();
        using var browser = api.CreateBrowser();

        // Expired.
        var (expiredId, expiredCode) = await RequestAndReadCodeAsync(browser, email, letter: 2);
        api.Time.Advance(PasswordReset.Lifetime);
        using (var late = await ConfirmAsync(browser, expiredId, expiredCode, _newPassword))
        {
            Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
            Assert.Equal(AccountEmailEndpoints.ConfirmationInvalidCode, await ProblemCodeAsync(late));
        }

        // A new code works once.
        var (resetId, code) = await RequestAndReadCodeAsync(browser, email, letter: 3);
        using (var reset = await ConfirmAsync(browser, resetId, code, _newPassword))
        {
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        }

        using (var again = await ConfirmAsync(api.CreateBrowser(), resetId, code, "another-3"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
            Assert.Equal(AccountEmailEndpoints.ConfirmationInvalidCode, await ProblemCodeAsync(again));
        }

        using var signedIn = await PasswordSignInAsync(api.CreateBrowser(), email, _newPassword);
        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
    }

    [Fact]
    public async Task WrongCodesSpendTheCodeAndAWeakPasswordCostsNoTry()
    {
        var (email, _) = await RegisteredAsync();
        using var browser = api.CreateBrowser();
        var (resetId, code) = await RequestAndReadCodeAsync(browser, email, letter: 2);

        for (var i = 0; i < PasswordReset.MaxAttempts + 2; i++)
        {
            using var weak = await ConfirmAsync(browser, resetId, code, "12345");
            Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
            var problem = await weak.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
        }

        for (var i = 0; i < PasswordReset.MaxAttempts; i++)
        {
            using var wrong = await ConfirmAsync(browser, resetId, code == "000000" ? "111111" : "000000", _newPassword);
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        using var spent = await ConfirmAsync(browser, resetId, code, _newPassword);
        Assert.Equal(HttpStatusCode.BadRequest, spent.StatusCode);
        using var old = await PasswordSignInAsync(api.CreateBrowser(), email, _password);
        Assert.Equal(HttpStatusCode.NoContent, old.StatusCode);
    }

    [Fact]
    public async Task RequestIsAnsweredAlikeForAnyAddressButOnlyAnAccountGetsALetter()
    {
        var (registered, _) = await RegisteredAsync();
        var unknown = NewAddress("nobody");
        using var browser = api.CreateBrowser();

        var answers = new List<string>();
        var ids = new List<Guid>();
        foreach (var address in new[] { registered, unknown })
        {
            using var sent = await RequestAsync(browser, address);
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
            var body = await sent.Content.ReadFromJsonAsync<JsonElement>(Ct);
            answers.Add(string.Join(',', body.EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}")));
            Assert.Equal(60, body.GetProperty("resendAfterSeconds").GetInt32());
            ids.Add(body.GetProperty("resetId").GetGuid());

            using var early = await RequestAsync(browser, address);
            Assert.Equal(HttpStatusCode.TooManyRequests, early.StatusCode);
            Assert.Equal(RegistrationEndpoints.CodeTooSoonCode, await ProblemCodeAsync(early));
        }

        Assert.Single(answers.Distinct());
        await LettersAsync(registered, 2);
        using var mailpit = api.Mailpit.CreateClient();
        Assert.Empty(await mailpit.SearchAsync(unknown, Ct));
        using var guessed = await ConfirmAsync(browser, ids[1], "123456", _newPassword);
        Assert.Equal(HttpStatusCode.BadRequest, guessed.StatusCode);

        using var invalid = await RequestAsync(browser, "not-an-email");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task UnconfirmedRegistrationGetsNoResetLetter()
    {
        var email = NewAddress("pending");
        using var client = api.CreateClient();
        (await RegisterAsync(client, email, _password)).Dispose();
        await LettersAsync(email, 1);

        using var sent = await RequestAsync(client, email);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);
        using var mailpit = api.Mailpit.CreateClient();
        Assert.Single(await mailpit.SearchAsync(email, Ct));
    }

    [Fact]
    public async Task OwnerChangesThePasswordWithTheCurrentOneAndKeepsTheSessions()
    {
        var (email, account) = await RegisteredAsync("Petr.Change");
        using var browser = api.CreateBrowser();
        (await PasswordSignInAsync(browser, email, _password)).Dispose();
        var tokens = await TokensAfterTheCodeFlowAsync(browser);
        var accessToken = tokens.GetProperty("access_token").GetString();

        using (var anonymous = await ChangeAsync(browser, null, _password, _newPassword))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        using (var wrong = await ChangeAsync(browser, accessToken, "not-my-password", _newPassword))
        {
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
            Assert.Equal(PasswordEndpoints.CurrentPasswordInvalidCode, await ProblemCodeAsync(wrong));
        }

        using (var weak = await ChangeAsync(browser, accessToken, _password, "12345"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
            var problem = await weak.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.True(problem.GetProperty("errors").TryGetProperty("newPassword", out _));
        }

        using (var changed = await ChangeAsync(browser, accessToken, _password, _newPassword))
        {
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        }

        // The owner proved who they are: the session goes on.
        var (status, _) = await OidcFlow.RefreshAsync(browser, tokens.GetProperty("refresh_token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(account, await WhoAmIAsync(await TokensAfterTheCodeFlowAsync(browser)));

        using (var old = await PasswordSignInAsync(api.CreateBrowser(), email, _password))
        {
            Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        }

        using (var signedIn = await PasswordSignInAsync(api.CreateBrowser(), email, _newPassword))
        {
            Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
        }

        var letters = await LettersAsync(email, 2);
        Assert.Equal("Пароль изменён", letters[0].Subject);
    }

    [Fact]
    public async Task AccountWithoutAPasswordCannotChangeItButCanSetOneByReset()
    {
        // Signed up with a code: no password yet.
        var account = await api.CreateAccountAsync();
        var email = NewAddress("codeonly");
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var stored = await db.Accounts.SingleAsync(a => a.Id == account.Id, Ct);
            stored.ConfirmEmail(email, EmailAddresses.Normalize(email)!, api.Time.GetUtcNow());
            await db.SaveChangesAsync(Ct);
        }

        using var browser = api.CreateBrowser();
        await OidcFlow.SignInAtProviderAsync(browser, account.Id);
        var accessToken = (await TokensAfterTheCodeFlowAsync(browser)).GetProperty("access_token").GetString();
        using (var refused = await ChangeAsync(browser, accessToken, "anything-1", _newPassword))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal(PasswordEndpoints.CurrentPasswordInvalidCode, await ProblemCodeAsync(refused));
        }

        var (resetId, code) = await RequestAndReadCodeAsync(browser, email, letter: 1);
        using (var reset = await ConfirmAsync(browser, resetId, code, _newPassword))
        {
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        }

        using var signedIn = await PasswordSignInAsync(api.CreateBrowser(), email, _newPassword);
        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
    }

    private static Task<HttpResponseMessage> PasswordSignInAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/sign-in/password", new { email, password }, Ct);

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/v1/password-reset", new { email }, Ct);

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, Guid resetId, string code, string password) =>
        client.PostAsJsonAsync($"/api/v1/password-reset/{resetId}/confirmation", new { code, password }, Ct);

    private static async Task<HttpResponseMessage> ChangeAsync(
        HttpClient client, string? accessToken, string currentPassword, string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/account/password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }

        return await client.SendAsync(request, Ct);
    }

    private async Task<(Guid ResetId, string Code)> RequestAndReadCodeAsync(HttpClient client, string email, int letter)
    {
        using var sent = await RequestAsync(client, email);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var resetId = (await sent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("resetId").GetGuid();
        return (resetId, await api.CodeFromMailpitAsync(email, letter));
    }

    // A person who registered with the password and confirmed the address: the address and the account.
    private async Task<(string Email, Guid Account)> RegisteredAsync(string name = "reader")
    {
        using var client = api.CreateClient();
        var email = NewAddress(name);
        var (registration, code) = await api.RegisterAndReadCodeAsync(client, email);
        using var confirmed = await RegistrationApi.ConfirmAsync(client, registration, code);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        return (email, registration);
    }

    // The letters to the address, newest first, once there are at least count of them.
    private async Task<IReadOnlyList<MailpitSummary>> LettersAsync(string email, int count)
    {
        using var mailpit = api.Mailpit.CreateClient();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            var letters = await mailpit.SearchAsync(email, Ct);
            if (letters.Count >= count)
            {
                return letters;
            }

            Assert.True(DateTime.UtcNow < deadline, $"letter {count} to {email} did not reach Mailpit within 30 seconds");
            await Task.Delay(100, Ct);
        }
    }

    private static async Task<JsonElement> TokensAfterTheCodeFlowAsync(HttpClient browser)
    {
        var pkce = Pkce.Create();
        var code = await OidcFlow.CodeAsync(browser, pkce);
        var (status, tokens) = await OidcFlow.ExchangeAsync(browser, code, pkce.Verifier);
        Assert.Equal(HttpStatusCode.OK, status);
        return tokens;
    }

    private async Task<Guid> WhoAmIAsync(JsonElement tokens)
    {
        using var client = api.CreateClient();
        using var whoAmI = await OidcFlow.WhoAmIAsync(client, tokens.GetProperty("access_token").GetString());
        return Guid.Parse(await whoAmI.Content.ReadAsStringAsync(Ct));
    }
}
