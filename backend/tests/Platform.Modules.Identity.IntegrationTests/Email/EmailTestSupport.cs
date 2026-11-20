using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Mail;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Identity.IntegrationTests.Email;

/// <summary>
/// The Api host with the Identity module on PostgreSQL and Redis, the test sign-in (<c>X-Test-Account</c>), a stand-in
/// for a publishing endpoint that requires <see cref="AccountPolicies.ConfirmedEmail"/>, and the letters the module
/// queues: they leave its outbox for the in-memory bus, where this factory catches them instead of the workers.
/// </summary>
public sealed partial class EmailApi : IntegrationApiFactory
{
    private readonly ConcurrentQueue<SendEmail> _letters = new();
    private bool _listening;

    public EmailApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) =>
        WithModule(services =>
        {
            services.AddTestAuthentication();
            services.AddApiEndpoints<PublishTestEndpoints>();
        });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Empties the database and Redis; the first call also starts catching letters.</summary>
    public async Task ResetAndListenAsync()
    {
        await ResetAsync();
        if (_listening)
        {
            return;
        }

        var handle = Services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            "identity-tests-letters",
            (_, endpoint) => endpoint.Handler<SendEmail>(context =>
            {
                _letters.Enqueue(context.Message);
                return Task.CompletedTask;
            }));
        await handle.Ready;
        _listening = true;
    }

    /// <summary>A new account without an email.</summary>
    public async Task<Guid> CreateAccountAsync()
    {
        var account = Account.Create(Time);
        await SaveAsync<IdentityDbContext>(account);
        return account.Id;
    }

    /// <summary>A request signed in as <paramref name="account"/>.</summary>
    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Guid? account, object? body = null)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(method, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, Ct);
    }

    public Task<HttpResponseMessage> ChangeEmailAsync(Guid account, string email) =>
        SendAsync(HttpMethod.Put, "/api/v1/account/email", account, new { email });

    public Task<HttpResponseMessage> ConfirmAsync(Guid account, string code) =>
        SendAsync(HttpMethod.Post, "/api/v1/account/email/confirmation", account, new { code });

    public Task<HttpResponseMessage> PublishAsync(Guid? account) =>
        SendAsync(HttpMethod.Post, PublishTestEndpoints.Path, account);

    public async Task<JsonElement> EmailStateAsync(Guid account)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/account/email", account);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    /// <summary>Sets the email and returns the code of the letter it gets.</summary>
    public async Task<string> RequestCodeAsync(Guid account, string email)
    {
        using var response = await ChangeEmailAsync(account, email);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
        return CodeOf(await LetterToAsync(email));
    }

    /// <summary>The last letter to <paramref name="recipient"/>, waiting for the outbox to deliver it.</summary>
    public async Task<SendEmail> LetterToAsync(string recipient, int count = 1)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            var letters = _letters.Where(l => l.To == recipient).ToList();
            if (letters.Count >= count)
            {
                return letters[^1];
            }

            Assert.True(DateTime.UtcNow < deadline, $"no letter to {recipient} within 30 seconds");
            await Task.Delay(100, Ct);
        }
    }

    public static string CodeOf(SendEmail letter) => SixDigits().Match(letter.TextBody).Value;

    public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}

/// <summary>A stand-in for publishing (posts and comments come later): allowed only with a confirmed email.</summary>
public sealed class PublishTestEndpoints : IApiEndpoints
{
    public const string Path = "/api/v1/test/publish";

    public void MapV1(IEndpointRouteBuilder v1) =>
        v1.MapPost("/test/publish", () => Results.NoContent()).RequireAuthorization(AccountPolicies.ConfirmedEmail);
}
