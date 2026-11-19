using Platform.Workers;

namespace Platform.Configuration.Tests;

/// <summary>The real hosts refuse to start without their required settings and name them (E01-13).</summary>
public sealed class HostConfigurationTests
{
    [Fact]
    public void WorkersHostDoesNotStartWithoutConnectionStringsAndNamesThem()
    {
        var e = Assert.Throws<InvalidConfigurationException>(
            () => WorkerHost.CreateBuilder(["--environment", PlatformEnvironments.Production]));

        Assert.Contains("ConnectionStrings:RabbitMq is required (environment variable ConnectionStrings__RabbitMq)", e.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings:Redis is required (environment variable ConnectionStrings__Redis)", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkersHostRejectsUnknownEnvironment()
    {
        var e = Assert.Throws<InvalidConfigurationException>(() => WorkerHost.CreateBuilder(
            ["--environment", "Prod", "--Messaging:Transport", "InMemory", "--Cache:Provider", "InMemory"]));

        Assert.Contains("environment 'Prod' is unknown", Assert.Single(e.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void WorkersHostTakesSecretsFromSecretFiles()
    {
        using var secrets = new TempDirectory();
        File.WriteAllText(Path.Combine(secrets.Path, "ConnectionStrings__RabbitMq"), "amqp://user:pass@rabbit:5672/");
        File.WriteAllText(Path.Combine(secrets.Path, "ConnectionStrings__Redis"), "redis:6379,password=pass");

        var builder = WorkerHost.CreateBuilder(
            ["--environment", PlatformEnvironments.Production, "--Secrets:Directory", secrets.Path]);

        Assert.Equal("redis:6379,password=pass", builder.Configuration["ConnectionStrings:Redis"]);
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
