using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Testing.Containers;
using Platform.Workers;

namespace Platform.Mail.Tests;

/// <summary>
/// Delivery of E01-12: a letter goes through the queue of the workers host, a failed delivery is retried, and the
/// SMTP sender configured by settings delivers it to Mailpit, where the test reads it.
/// </summary>
public sealed class DeliveryTests(MailpitFixture mailpit)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private static readonly Dictionary<string, string> _values = new() { ["code"] = "482913", ["lifetime"] = "10 минут" };

    [Fact]
    public async Task LetterGoesThroughTheQueueToTheSender()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = Worker(new() { ["Mail:Provider"] = "InMemory" });
        await host.StartAsync(ct);
        var sender = host.Services.GetRequiredService<InMemoryEmailSender>();

        await SendAsync(host, "reader@example.org", ct);

        await Eventually(() => sender.Sent.Count == 1, "the letter is delivered");
        var email = Assert.Single(sender.Sent);
        Assert.Equal("login-code", email.Template);
        Assert.Equal("reader@example.org", email.To);
        Assert.Equal("Код для входа: 482913", email.Subject);
        Assert.Contains("482913", email.TextBody, StringComparison.Ordinal);
        await host.StopAsync(ct);
    }

    [Fact]
    public async Task FailedDeliveryIsRetriedUntilItSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var flaky = new FlakySender(failures: 2);
        using var host = Worker(new() { ["Mail:Provider"] = "InMemory" }, s => s.AddSingleton<IEmailSender>(flaky));
        await host.StartAsync(ct);

        await SendAsync(host, "retry@example.org", ct);

        await Eventually(() => flaky.Delivered.Count == 1, "the third attempt delivers the letter");
        Assert.Equal(3, flaky.Attempts);
        Assert.Equal("retry@example.org", Assert.Single(flaky.Delivered).To);
        await host.StopAsync(ct);
    }

    [Fact]
    public async Task InvalidAddressFailsInTheCallerNotInTheQueue()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = Worker(new() { ["Mail:Provider"] = "InMemory" });
        await host.StartAsync(ct);

        await Assert.ThrowsAsync<ArgumentException>(() => SendAsync(host, "not an address", ct));
        await host.StopAsync(ct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("mailer")]
    public async Task SmtpFromSettingsDeliversToMailpit(string? username)
    {
        var ct = TestContext.Current.CancellationToken;
        var recipient = $"{Guid.NewGuid():N}@example.org";
        using var host = Worker(new()
        {
            ["Mail:Provider"] = "Smtp",
            ["Mail:Smtp:Host"] = mailpit.SmtpHost,
            ["Mail:Smtp:Port"] = mailpit.SmtpMappedPort.ToString(CultureInfo.InvariantCulture),
            ["Mail:Smtp:Security"] = "None",
            ["Mail:Smtp:Username"] = username,
            ["Mail:Smtp:Password"] = username is null ? null : "secret",
            ["Mail:From:Address"] = "noreply@platform.test",
        });
        await host.StartAsync(ct);
        using var client = mailpit.CreateClient();

        await SendAsync(host, recipient, ct);

        var letter = await client.WaitForAsync(recipient, _timeout, ct);
        Assert.Equal("Код для входа: 482913", letter.Subject);
        Assert.Equal("noreply@platform.test", letter.From.Address);
        Assert.Equal(MailBrand.Default.ProductName, letter.From.Name);
        Assert.Equal(recipient, Assert.Single(letter.To).Address);
        Assert.Contains("482913", letter.Text, StringComparison.Ordinal);
        Assert.Contains("482913", letter.Html, StringComparison.Ordinal);
        Assert.EndsWith("@platform.test", letter.MessageId, StringComparison.Ordinal);
        await host.StopAsync(ct);
    }

    [Fact]
    public async Task SenderNameIsSetBySettings()
    {
        var ct = TestContext.Current.CancellationToken;
        var recipient = $"{Guid.NewGuid():N}@example.org";
        using var host = Worker(new()
        {
            ["Mail:Smtp:Host"] = mailpit.SmtpHost,
            ["Mail:Smtp:Port"] = mailpit.SmtpMappedPort.ToString(CultureInfo.InvariantCulture),
            ["Mail:Smtp:Security"] = "None",
            ["Mail:From:Address"] = "robot@platform.test",
            ["Mail:From:Name"] = "Робот",
        });
        await host.StartAsync(ct);
        using var client = mailpit.CreateClient();

        await SendAsync(host, recipient, ct);

        var letter = await client.WaitForAsync(recipient, _timeout, ct);
        Assert.Equal(new MailpitAddress("Робот", "robot@platform.test"), letter.From);
        await host.StopAsync(ct);
    }

    private static async Task SendAsync(IHost host, string recipient, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMailer>()
            .SendAsync(recipient, "login-code", CultureInfo.GetCultureInfo("ru-RU"), _values, ct);
    }

    /// <summary>The workers host (<see cref="WorkerHost"/>) on the in-memory bus and cache, with these mail settings.</summary>
    private static IHost Worker(Dictionary<string, string?> mail, Action<IServiceCollection>? services = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = "Test",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(mail)
        {
            ["Messaging:Transport"] = "InMemory",
            ["Cache:Provider"] = "InMemory",
        });
        WorkerHost.Configure(builder.Services, builder.Configuration);
        services?.Invoke(builder.Services);
        return builder.Build();
    }

    private static async Task Eventually(Func<bool> condition, string because)
    {
        using var cts = new CancellationTokenSource(_timeout);
        while (!condition())
        {
            if (cts.IsCancellationRequested)
            {
                Assert.Fail($"timed out after {_timeout.TotalSeconds:0}s waiting until {because}");
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A mail service that is down for the first <c>failures</c> attempts.</summary>
    private sealed class FlakySender(int failures) : IEmailSender
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        public List<SendEmail> Delivered { get; } = [];

        public Task SendAsync(SendEmail email, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attempts) <= failures)
            {
                throw new IOException("the mail service is unavailable");
            }

            lock (Delivered)
            {
                Delivered.Add(email);
            }

            return Task.CompletedTask;
        }
    }
}
