using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Mail;
using Platform.Modules.Identity.Email;
using static Platform.Modules.Identity.IntegrationTests.Email.EmailApi;

namespace Platform.Modules.Identity.IntegrationTests.Email;

/// <summary>
/// The account's email through the API (FR-ID, ADR-0020): unique after normalisation, confirmed by a code from a letter,
/// required to publish, and the only kind of email a merge of accounts may match by.
/// </summary>
public sealed class AccountEmailTests(EmailApi api) : IClassFixture<EmailApi>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAndListenAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CodeFromTheLetterConfirmsTheEmailAndStoresTheDate()
    {
        var account = await api.CreateAccountAsync();

        var letter = await RequestAsync(account, "Ivan.Petrov@Example.com");
        Assert.Equal(AccountEmails.Template, letter.Template);
        Assert.Contains("30 минут", letter.TextBody, StringComparison.Ordinal);
        var before = await api.EmailStateAsync(account);
        Assert.Equal("Ivan.Petrov@Example.com", before.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("confirmedAt").ValueKind);

        api.Time.Advance(TimeSpan.FromMinutes(7));
        using var confirmed = await api.ConfirmAsync(account, CodeOf(letter));

        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        var stored = await StoredAsync(account);
        Assert.Equal("Ivan.Petrov@Example.com", stored.Email);
        Assert.Equal("ivan.petrov@example.com", stored.NormalizedEmail);
        Assert.Equal(api.Time.GetUtcNow(), stored.EmailConfirmedAt);
        Assert.Equal(api.Time.GetUtcNow(), (await api.EmailStateAsync(account)).GetProperty("confirmedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task UnconfirmedAccountCannotPublish()
    {
        var account = await api.CreateAccountAsync();
        using (var withoutEmail = await api.PublishAsync(account))
        {
            Assert.Equal(HttpStatusCode.Forbidden, withoutEmail.StatusCode);
            Assert.Equal(AccountPolicies.EmailNotConfirmedCode, await ProblemCodeAsync(withoutEmail));
        }

        var code = await api.RequestCodeAsync(account, "writer@example.com");
        using (var unconfirmed = await api.PublishAsync(account))
        {
            Assert.Equal(HttpStatusCode.Forbidden, unconfirmed.StatusCode);
            Assert.Equal(AccountPolicies.EmailNotConfirmedCode, await ProblemCodeAsync(unconfirmed));
        }

        using (var anonymous = await api.PublishAsync(null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        (await api.ConfirmAsync(account, code)).Dispose();
        using var confirmed = await api.PublishAsync(account);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
    }

    [Theory]
    [InlineData("Anna.Smith@Example.com", "anna.smith@EXAMPLE.COM")]
    [InlineData("J.Doe@gmail.com", "jdoe@GoogleMail.com")]
    [InlineData("jdoe@gmail.com", "J.D.O.E@Gmail.com")]
    public async Task SecondAccountCannotTakeTheSameEmailAfterNormalisation(string first, string second)
    {
        var owner = await api.CreateAccountAsync();
        var other = await api.CreateAccountAsync();
        var code = await api.RequestCodeAsync(owner, first);

        using (var unconfirmedTaken = await api.ChangeEmailAsync(other, second))
        {
            Assert.Equal(HttpStatusCode.Conflict, unconfirmedTaken.StatusCode);
            Assert.Equal(AccountEmailEndpoints.EmailTakenCode, await ProblemCodeAsync(unconfirmedTaken));
        }

        (await api.ConfirmAsync(owner, code)).Dispose();
        using var confirmedTaken = await api.ChangeEmailAsync(other, second);
        Assert.Equal(HttpStatusCode.Conflict, confirmedTaken.StatusCode);
        Assert.Null((await StoredAsync(other)).NormalizedEmail);
    }

    [Fact]
    public async Task DatabaseRefusesTwoAccountsWithOneNormalisedEmail()
    {
        var first = Account.Create(api.Time);
        var second = Account.Create(api.Time);
        first.SetUnconfirmedEmail("Anna@Example.com", "anna@example.com");
        second.SetUnconfirmedEmail("anna@example.com", "anna@example.com");
        await api.SaveAsync<IdentityDbContext>(first);

        var e = await Assert.ThrowsAsync<DbUpdateException>(() => api.SaveAsync<IdentityDbContext>(second));
        Assert.Contains(IdentityDbContext.NormalizedEmailIndex, e.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnconfirmedEmailIsNotAMergeKey()
    {
        var account = await api.CreateAccountAsync();
        var code = await api.RequestCodeAsync(account, "Jane.Roe@gmail.com");

        Assert.Null(await FindByConfirmedEmailAsync("janeroe@gmail.com"));

        (await api.ConfirmAsync(account, code)).Dispose();
        Assert.Equal(account, await FindByConfirmedEmailAsync("j.a.n.e.roe@googlemail.com"));
        Assert.Null(await FindByConfirmedEmailAsync("janeroe+other@gmail.com"));
    }

    [Fact]
    public async Task ExpiredCodeIsRefused()
    {
        var account = await api.CreateAccountAsync();
        var code = await api.RequestCodeAsync(account, "late@example.com");

        api.Time.Advance(EmailConfirmation.Lifetime);
        using var response = await api.ConfirmAsync(account, code);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AccountEmailEndpoints.ConfirmationInvalidCode, await ProblemCodeAsync(response));
        Assert.Null((await StoredAsync(account)).EmailConfirmedAt);
    }

    [Fact]
    public async Task CodeOfAnotherAccountIsRefused()
    {
        var owner = await api.CreateAccountAsync();
        var other = await api.CreateAccountAsync();
        var ownersCode = await api.RequestCodeAsync(owner, "owner@example.com");
        var othersCode = await api.RequestCodeAsync(other, "other@example.com");
        Assert.NotEqual(ownersCode, othersCode);

        using var foreign = await api.ConfirmAsync(other, ownersCode);
        using var withoutRequest = await api.ConfirmAsync(await api.CreateAccountAsync(), ownersCode);

        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutRequest.StatusCode);
        Assert.Null((await StoredAsync(other)).EmailConfirmedAt);
        using var own = await api.ConfirmAsync(owner, ownersCode);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
    }

    [Fact]
    public async Task CodeIsSpentAfterFiveWrongAttemptsAndANewRequestReplacesIt()
    {
        var account = await api.CreateAccountAsync();
        var first = await api.RequestCodeAsync(account, "guess@example.com");
        var wrong = first == "000000" ? "000001" : "000000";
        for (var i = 0; i < EmailConfirmation.MaxAttempts; i++)
        {
            (await api.ConfirmAsync(account, wrong)).Dispose();
        }

        using (var spent = await api.ConfirmAsync(account, first))
        {
            Assert.Equal(HttpStatusCode.BadRequest, spent.StatusCode);
        }

        using (var again = await api.ChangeEmailAsync(account, "guess@example.com"))
        {
            Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        }

        var second = CodeOf(await api.LetterToAsync("guess@example.com", count: 2));
        if (second != first)
        {
            using var old = await api.ConfirmAsync(account, first);
            Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        }

        using var confirmed = await api.ConfirmAsync(account, second);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
    }

    [Fact]
    public async Task ConfirmedEmailStaysUntilTheNewOneIsConfirmed()
    {
        var account = await api.CreateAccountAsync();
        (await api.ConfirmAsync(account, await api.RequestCodeAsync(account, "old@example.com"))).Dispose();

        var code = await api.RequestCodeAsync(account, "new@example.com");

        var pending = await api.EmailStateAsync(account);
        Assert.Equal("old@example.com", pending.GetProperty("email").GetString());
        Assert.Equal("new@example.com", pending.GetProperty("pendingEmail").GetString());
        using (var stillAllowed = await api.PublishAsync(account))
        {
            Assert.Equal(HttpStatusCode.NoContent, stillAllowed.StatusCode);
        }

        using (var same = await api.ChangeEmailAsync(account, "OLD@example.com"))
        {
            Assert.Equal(HttpStatusCode.NoContent, same.StatusCode);
        }

        (await api.ConfirmAsync(account, code)).Dispose();
        Assert.Equal("new@example.com", (await StoredAsync(account)).NormalizedEmail);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("anna@localhost")]
    public async Task InvalidAddressIsAValidationError(string email)
    {
        var account = await api.CreateAccountAsync();

        using var response = await api.ChangeEmailAsync(account, email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task AnonymousRequestsAreRefused()
    {
        using var change = await api.SendAsync(HttpMethod.Put, "/api/v1/account/email", null, new { email = "a@example.com" });
        using var confirm = await api.SendAsync(HttpMethod.Post, "/api/v1/account/email/confirmation", null, new { code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, confirm.StatusCode);
    }

    [Fact]
    public async Task CodesAreLimitedToFiveLettersAnHour()
    {
        var account = await api.CreateAccountAsync();
        List<HttpStatusCode> statuses = [];
        for (var i = 0; i < 6; i++)
        {
            using var response = await api.ChangeEmailAsync(account, $"flood{i}@example.com");
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Accepted, 5), HttpStatusCode.TooManyRequests], statuses);
    }

    private async Task<SendEmail> RequestAsync(Guid account, string email)
    {
        using var response = await api.ChangeEmailAsync(account, email);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await api.LetterToAsync(email);
    }

    private async Task<Account> StoredAsync(Guid account)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Accounts.AsNoTracking()
            .SingleAsync(a => a.Id == account, Ct);
    }

    private async Task<Guid?> FindByConfirmedEmailAsync(string email)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AccountEmails>().FindByConfirmedEmailAsync(email, Ct);
    }
}
