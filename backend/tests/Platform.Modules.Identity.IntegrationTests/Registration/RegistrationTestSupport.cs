using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Platform.Mail;
using Platform.Modules.Identity.IntegrationTests.SignIn;
using Platform.Testing.Containers;

namespace Platform.Modules.Identity.IntegrationTests.Registration;

/// <summary>
/// The Api host with the Identity module, its clients, and the delivery of its letters the way the workers deliver them:
/// the letters leave the module's outbox for the in-memory bus, and the SMTP sender of <c>Mail:Smtp:*</c> takes them to
/// Mailpit, where the tests read the codes (ADR-0017). Tests share one Mailpit, so each registers its own address.
/// </summary>
public sealed partial class RegistrationApi : IdentityApi
{
    private readonly MailpitFixture _mailpit;
    private bool _delivering;

    public RegistrationApi(PostgresFixture postgres, RedisFixture redis, MailpitFixture mailpit)
        : base(postgres, redis)
    {
        _mailpit = mailpit;
        WithSetting("Mail:Provider", "Smtp");
        WithSetting("Mail:Smtp:Host", mailpit.SmtpHost);
        WithSetting("Mail:Smtp:Port", mailpit.SmtpMappedPort.ToString(CultureInfo.InvariantCulture));
        WithSetting("Mail:Smtp:Security", "None");
        WithSetting("Mail:From:Address", "robot@platform.test");
        WithModule((services, configuration) => services.AddPlatformMailDelivery(configuration));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A new address for one test: Mailpit keeps the letters of every test.</summary>
    public static string NewAddress(string name = "reader") => $"{name}.{Guid.NewGuid():N}@example.com";

    /// <summary>Empties the database and Redis, writes the clients, and on the first call starts delivering letters.</summary>
    public async Task ResetAndDeliverAsync()
    {
        await ResetWithClientsAsync();
        if (_delivering)
        {
            return;
        }

        var sender = Services.GetRequiredService<IEmailSender>();
        var handle = Services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            "identity-tests-delivery",
            (_, endpoint) => endpoint.Handler<SendEmail>(context => sender.SendAsync(context.Message, context.CancellationToken)));
        await handle.Ready;
        _delivering = true;
    }

    public static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string? email, string? password = "secret-1") =>
        await client.PostAsJsonAsync("/api/v1/registration", new { email, password }, Ct);

    public static async Task<HttpResponseMessage> ResendAsync(HttpClient client, Guid registrationId) =>
        await client.PostAsync($"/api/v1/registration/{registrationId}/code", null, Ct);

    public static async Task<HttpResponseMessage> ConfirmAsync(HttpClient client, Guid registrationId, string code) =>
        await client.PostAsJsonAsync($"/api/v1/registration/{registrationId}/confirmation", new { code }, Ct);

    /// <summary>Registers <paramref name="email"/>: the registration and the code of its letter, read from Mailpit.</summary>
    public async Task<(Guid RegistrationId, string Code)> RegisterAndReadCodeAsync(HttpClient client, string email)
    {
        using var response = await RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return (body.GetProperty("registrationId").GetGuid(), await CodeFromMailpitAsync(email));
    }

    /// <summary>The code of letter number <paramref name="count"/> to <paramref name="email"/>, waiting for Mailpit.</summary>
    public async Task<string> CodeFromMailpitAsync(string email, int count = 1)
    {
        using var mailpit = _mailpit.CreateClient();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            var letters = await mailpit.SearchAsync(email, Ct);
            if (letters.Count >= count)
            {
                var letter = await mailpit.GetAsync(letters[0].Id, Ct);
                return SixDigits().Match(letter.Text).Value;
            }

            Assert.True(DateTime.UtcNow < deadline, $"letter {count} to {email} did not reach Mailpit within 30 seconds");
            await Task.Delay(100, Ct);
        }
    }

    public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}
