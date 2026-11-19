using Microsoft.Extensions.Configuration;

namespace Platform.Configuration.Tests;

public sealed class ConfigurationValidatorTests
{
    private static readonly Setting _mode = new("Feature:Mode", "how the feature runs")
    {
        AllowedValues = ["Real", "Fake"],
    };

    private static readonly Setting _password = new("ConnectionStrings:Thing", "the thing")
    {
        Secret = true,
        RequiredWhen = c => !string.Equals(c["Feature:Mode"], "Fake", StringComparison.OrdinalIgnoreCase),
    };

    private static readonly Setting[] _settings = [_mode, _password];

    [Fact]
    public void MissingRequiredSettingIsNamedWithItsEnvironmentVariable()
    {
        var problems = ConfigurationValidator.Validate(Build([]), PlatformEnvironments.Production, _settings);

        var problem = Assert.Single(problems);
        Assert.Contains("ConnectionStrings:Thing is required", problem, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__Thing", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ConditionalSettingIsNotRequiredWhenItsConditionDoesNotHold()
    {
        var problems = ConfigurationValidator.Validate(
            Build(new() { ["Feature:Mode"] = "fake" }), PlatformEnvironments.Production, _settings);

        Assert.Empty(problems);
    }

    [Fact]
    public void ValueOutsideAllowedValuesIsRejected()
    {
        var problems = ConfigurationValidator.Validate(
            Build(new() { ["Feature:Mode"] = "Maybe", ["ConnectionStrings:Thing"] = "x" }),
            PlatformEnvironments.Production,
            _settings);

        Assert.Equal("Feature:Mode = 'Maybe': expected one of Real, Fake", Assert.Single(problems));
    }

    [Fact]
    public void UnknownEnvironmentIsRejected()
    {
        var problems = ConfigurationValidator.Validate(
            Build(new() { ["ConnectionStrings:Thing"] = "x" }), "Prod", _settings);

        Assert.Contains("environment 'Prod' is unknown", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureValidThrowsWithEveryProblem()
    {
        var e = Assert.Throws<InvalidConfigurationException>(() => ConfigurationValidator.EnsureValid(
            Build(new() { ["Feature:Mode"] = "Maybe" }), PlatformEnvironments.Staging, _settings));

        Assert.Equal(2, e.Problems.Count);
        Assert.Contains("Feature:Mode", e.Message, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings:Thing", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PlatformEnvironments.Staging, true)]
    [InlineData(PlatformEnvironments.Production, true)]
    [InlineData(PlatformEnvironments.Development, false)]
    [InlineData(PlatformEnvironments.Test, false)]
    public void SecretFromSettingsFileIsRejectedOutsideDevelopmentAndTest(string environment, bool rejected)
    {
        using var directory = new TempDirectory();
        File.WriteAllText(
            Path.Combine(directory.Path, "appsettings.json"),
            """{ "ConnectionStrings": { "Thing": "Password=very-secret-value" } }""");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory.Path)
            .AddJsonFile("appsettings.json")
            .Build();

        var problems = ConfigurationValidator.Validate(configuration, environment, _settings);

        Assert.Equal(rejected, problems.Count == 1);
        Assert.DoesNotContain(problems, p => p.Contains("very-secret-value", StringComparison.Ordinal));
        if (rejected)
        {
            Assert.Contains("is a secret but comes from appsettings.json", problems[0], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SecretOverriddenByEnvironmentVariableIsAccepted()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(
            Path.Combine(directory.Path, "appsettings.json"),
            """{ "ConnectionStrings": { "Thing": "from-file" } }""");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory.Path)
            .AddJsonFile("appsettings.json")
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Thing"] = "from-environment" })
            .Build();

        Assert.Empty(ConfigurationValidator.Validate(configuration, PlatformEnvironments.Production, _settings));
    }

    [Fact]
    public void SecretFilesSupplySettingsByEnvironmentVariableName()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "ConnectionStrings__Thing"), "Password=from-file\n");
        File.WriteAllText(Path.Combine(directory.Path, "..data"), "kubernetes service entry");
        var configuration = new ConfigurationBuilder().Add(new SecretFilesConfigurationSource(directory.Path)).Build();

        Assert.Equal("Password=from-file", configuration["ConnectionStrings:Thing"]);
        Assert.DoesNotContain(configuration.AsEnumerable(), pair => pair.Key.StartsWith('.'));
        Assert.Empty(ConfigurationValidator.Validate(configuration, PlatformEnvironments.Production, _settings));
    }

    [Fact]
    public void MissingSecretFilesDirectoryIsAConfigurationError()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var e = Assert.Throws<InvalidConfigurationException>(
            () => new ConfigurationBuilder().Add(new SecretFilesConfigurationSource(missing)).Build());

        Assert.Contains("Secrets:Directory", e.Message, StringComparison.Ordinal);
    }

    private static IConfigurationRoot Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
