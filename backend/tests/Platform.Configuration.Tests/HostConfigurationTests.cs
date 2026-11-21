using Platform.Workers;

namespace Platform.Configuration.Tests;

/// <summary>The real hosts refuse to start without their required settings and name them (E01-13).</summary>
public sealed class HostConfigurationTests
{
    private const string _database = "--ConnectionStrings:Platform=Host=localhost;Database=never_contacted";

    [Fact]
    public void WorkersHostDoesNotStartWithoutConnectionStringsAndNamesThem()
    {
        var e = Assert.Throws<InvalidConfigurationException>(
            () => WorkerHost.CreateBuilder(["--environment", PlatformEnvironments.Production]));

        Assert.Contains("ConnectionStrings:RabbitMq is required (environment variable ConnectionStrings__RabbitMq)", e.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings:Redis is required (environment variable ConnectionStrings__Redis)", e.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings:Platform is required (environment variable ConnectionStrings__Platform)", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkersHostDoesNotStartWithoutSmtpSettingsAndNamesThem()
    {
        var e = Assert.Throws<InvalidConfigurationException>(() => WorkerHost.CreateBuilder(
            ["--environment", PlatformEnvironments.Production, "--Messaging:Transport", "InMemory", "--Cache:Provider", "InMemory", _database]));

        Assert.Contains("Mail:Smtp:Host is required (environment variable Mail__Smtp__Host)", e.Message, StringComparison.Ordinal);
        Assert.Contains("Mail:From:Address is required (environment variable Mail__From__Address)", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkersHostWithFakeMailNeedsNoSmtpSettings()
    {
        var builder = WorkerHost.CreateBuilder(
            ["--environment", PlatformEnvironments.Production, "--Messaging:Transport", "InMemory",
                "--Cache:Provider", "InMemory", "--Mail:Provider", "InMemory", _database]);

        Assert.Equal("InMemory", builder.Configuration["Mail:Provider"]);
    }

    [Fact]
    public void WorkersHostRejectsUnknownMailProvider()
    {
        var e = Assert.Throws<InvalidConfigurationException>(() => WorkerHost.CreateBuilder(
            ["--environment", PlatformEnvironments.Production, "--Messaging:Transport", "InMemory",
                "--Cache:Provider", "InMemory", "--Mail:Provider", "Sendgrid", _database]));

        Assert.Contains(e.Problems, p => p.Contains("Mail:Provider", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkersHostRejectsUnknownEnvironment()
    {
        var e = Assert.Throws<InvalidConfigurationException>(() => WorkerHost.CreateBuilder(
            ["--environment", "Prod", "--Messaging:Transport", "InMemory", "--Cache:Provider", "InMemory", "--Mail:Provider", "InMemory", _database]));

        Assert.Contains("environment 'Prod' is unknown", Assert.Single(e.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void WorkersHostTakesSecretsFromSecretFiles()
    {
        using var secrets = new TempDirectory();
        File.WriteAllText(Path.Combine(secrets.Path, "ConnectionStrings__RabbitMq"), "amqp://user:pass@rabbit:5672/");
        File.WriteAllText(Path.Combine(secrets.Path, "ConnectionStrings__Redis"), "redis:6379,password=pass");
        File.WriteAllText(Path.Combine(secrets.Path, "Mail__Smtp__Password"), "smtp-pass");

        var builder = WorkerHost.CreateBuilder(
            ["--environment", PlatformEnvironments.Production, "--Secrets:Directory", secrets.Path,
                "--Mail:Smtp:Host", "smtp.example.org", "--Mail:From:Address", "noreply@example.org", _database]);

        Assert.Equal("redis:6379,password=pass", builder.Configuration["ConnectionStrings:Redis"]);
        Assert.Equal("smtp-pass", builder.Configuration["Mail:Smtp:Password"]);
    }

    [Fact]
    public async Task MigratorWithoutConnectionStringNamesIt()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Migrator.MigratorApp.RunAsync(
            ["status", "--environment", PlatformEnvironments.Staging, "--ConnectionStrings:Platform="], output, error);

        Assert.Equal(2, exit);
        Assert.Contains("ConnectionStrings__Platform", error.ToString(), StringComparison.Ordinal);
    }
}
