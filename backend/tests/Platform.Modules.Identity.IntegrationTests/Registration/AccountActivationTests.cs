using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Identity.Contracts;
using static Platform.Modules.Identity.IntegrationTests.Registration.RegistrationApi;

namespace Platform.Modules.Identity.IntegrationTests.Registration;

/// <summary>
/// E04-01 (ADR-0024): Identity tells other modules about an account once it is here to stay — its email is confirmed
/// for the first time — through the module outbox, so the Blogs module can give it a personal blog. A registration
/// waiting for its code is not announced; a later sign-in of the same account is not announced again.
/// </summary>
public sealed class AccountActivationTests(RegistrationApi api) : IClassFixture<RegistrationApi>, IAsyncLifetime
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private readonly ConcurrentQueue<AccountActivated> _events = new();
    private HostReceiveEndpointHandle? _endpoint;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAndDeliverAsync();
        _endpoint = api.Services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            $"identity-tests-activations-{Guid.NewGuid():N}",
            (_, endpoint) => endpoint.Handler<AccountActivated>(context =>
            {
                _events.Enqueue(context.Message);
                return Task.CompletedTask;
            }));
        await _endpoint.Ready;
    }

    public async ValueTask DisposeAsync()
    {
        if (_endpoint is not null)
        {
            await _endpoint.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ConfirmedRegistrationIsAnnouncedOnceAndAPendingOneIsNot()
    {
        using var client = api.CreateClient();
        var email = NewAddress("activated");
        var (registration, code) = await api.RegisterAndReadCodeAsync(client, email);
        // The letter has left the outbox, so an event saved with the registration would have left too.
        Assert.DoesNotContain(_events, e => e.AccountId == registration);

        using (var confirmed = await ConfirmAsync(client, registration, code))
        {
            Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        }

        var activated = await EventuallyAsync(registration);
        Assert.Equal(api.Time.GetUtcNow(), activated.ActivatedAt);

        // Signing in again later announces nothing new.
        using (var signedIn = await client.PostAsJsonAsync("/api/v1/sign-in/password", new { email, password = "secret-1" }, Ct))
        {
            Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
        }

        var other = await SignInWithCodeAsync(client, NewAddress("marker"));
        await EventuallyAsync(other);
        Assert.Single(_events, e => e.AccountId == registration);
    }

    [Fact]
    public async Task AccountCreatedBySignInWithACodeIsAnnounced()
    {
        using var client = api.CreateClient();

        var account = await SignInWithCodeAsync(client, NewAddress("by-code"));

        await EventuallyAsync(account);
    }

    // Signs in with a one-time code to an address nobody has: the account the code created.
    private async Task<Guid> SignInWithCodeAsync(HttpClient client, string email)
    {
        using (var sent = await client.PostAsJsonAsync("/api/v1/sign-in/code", new { email }, Ct))
        {
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
            var challenge = (await sent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("challengeId").GetGuid();
            var code = await api.CodeFromMailpitAsync(email);
            using var confirmed = await client.PostAsJsonAsync($"/api/v1/sign-in/code/{challenge}/confirmation", new { code }, Ct);
            Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        }

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.Accounts.Where(a => a.Email == email).Select(a => a.Id).SingleAsync(Ct);
    }

    private async Task<AccountActivated> EventuallyAsync(Guid account)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (true)
        {
            if (_events.FirstOrDefault(e => e.AccountId == account) is { } activated)
            {
                return activated;
            }

            Assert.True(DateTime.UtcNow < deadline, $"no AccountActivated for {account} within {_timeout.TotalSeconds:0}s");
            await Task.Delay(100, Ct);
        }
    }
}
