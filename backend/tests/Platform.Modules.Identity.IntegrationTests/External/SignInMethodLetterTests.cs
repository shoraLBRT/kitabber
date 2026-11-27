using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.External;
using Platform.Modules.Identity.IntegrationTests.Email;

namespace Platform.Modules.Identity.IntegrationTests.External;

/// <summary>
/// The letters about the ways to sign in (E03-08, #119): linking a provider and unlinking it in the settings each tell
/// the owner at the confirmed email, through the module's outbox; a link or unlink that changed nothing sends nothing.
/// </summary>
public sealed class SignInMethodLetterTests(EmailApi api) : IClassFixture<EmailApi>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAndListenAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task LinkingAndUnlinkingAreToldByLetter()
    {
        var (account, email) = await CreateConfirmedAccountAsync();
        var subject = Guid.NewGuid().ToString("N");

        Assert.Equal(LinkOutcome.Linked, await LinkAsync(account, ExternalProviders.Yandex, subject));
        var linked = await api.LetterToAsync(email);
        Assert.Equal(ExternalIdentities.LinkedTemplate, linked.Template);
        Assert.Contains("Яндекс", linked.TextBody, StringComparison.Ordinal);
        Assert.Contains("Яндекс", linked.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain(subject, linked.TextBody, StringComparison.Ordinal);

        // The same pair again changes nothing and tells nothing.
        Assert.Equal(LinkOutcome.AlreadyLinked, await LinkAsync(account, ExternalProviders.Yandex, subject));

        api.Time.Advance(TimeSpan.FromMinutes(5));
        var identity = await OnlyIdentityAsync(account);
        using (var response = await api.SendAsync(HttpMethod.Delete, $"/api/v1/account/identities/{identity}", account))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var unlinked = await api.LetterToAsync(email, count: 2);
        Assert.Equal(ExternalIdentities.UnlinkedTemplate, unlinked.Template);
        Assert.Contains("Яндекс", unlinked.TextBody, StringComparison.Ordinal);
        Assert.Equal(
            [ExternalIdentities.LinkedTemplate, ExternalIdentities.UnlinkedTemplate],
            api.LettersTo(email).Select(l => l.Template));
    }

    [Fact]
    public async Task RefusedChangesSendNoLetter()
    {
        var (owner, ownerEmail) = await CreateConfirmedAccountAsync();
        var (other, otherEmail) = await CreateConfirmedAccountAsync();
        var subject = Guid.NewGuid().ToString("N");
        await LinkAsync(owner, ExternalProviders.Vk, subject);
        var identity = await OnlyIdentityAsync(owner);

        // Another account's pair is not moved, and another account's identity is not unlinked by someone else.
        Assert.Equal(LinkOutcome.TakenByAnotherAccount, await LinkAsync(other, ExternalProviders.Vk, subject));
        using (var response = await api.SendAsync(HttpMethod.Delete, $"/api/v1/account/identities/{identity}", other))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // The last way to sign in of an account without an email stays, and such an account has nowhere to be told.
        var withoutEmail = await api.CreateAccountAsync();
        await LinkAsync(withoutEmail, ExternalProviders.Telegram, Guid.NewGuid().ToString("N"));
        using (var response = await api.SendAsync(
            HttpMethod.Delete, $"/api/v1/account/identities/{await OnlyIdentityAsync(withoutEmail)}", withoutEmail))
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        // A change of the other account's own arrives after anything the refusals could have sent: they sent nothing.
        await LinkAsync(other, ExternalProviders.Google, Guid.NewGuid().ToString("N"));
        Assert.Equal(ExternalIdentities.LinkedTemplate, (await api.LetterToAsync(otherEmail)).Template);
        Assert.Single(api.LettersTo(otherEmail));
        Assert.Single(api.LettersTo(ownerEmail));
    }

    private async Task<(Guid Id, string Email)> CreateConfirmedAccountAsync()
    {
        var account = Account.Create(api.Time);
        var email = $"{Guid.NewGuid():N}@example.com";
        account.ConfirmEmail(email, email, api.Time.GetUtcNow());
        await api.SaveAsync<IdentityDbContext>(account);
        return (account.Id, email);
    }

    private async Task<LinkOutcome> LinkAsync(Guid accountId, string provider, string subject)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ExternalIdentities>()
            .LinkAsync(accountId, provider, subject, Ct);
    }

    private async Task<Guid> OnlyIdentityAsync(Guid accountId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ExternalIdentities>().ListAsync(accountId, Ct)).Single().Id;
    }
}
