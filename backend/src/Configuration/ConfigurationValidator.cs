using Microsoft.Extensions.Configuration;

namespace Platform.Configuration;

/// <summary>Checks the configuration against the declared <see cref="Setting"/>s before a host is built (ADR-0007).</summary>
public static class ConfigurationValidator
{
    /// <summary>Every problem found, one line each; empty when the configuration is valid. Secret values are never shown.</summary>
    public static IReadOnlyList<string> Validate(
        IConfiguration configuration,
        string environmentName,
        IEnumerable<Setting> settings)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(settings);
        var problems = new List<string>();
        if (!PlatformEnvironments.IsKnown(environmentName))
        {
            problems.Add(
                $"environment '{environmentName}' is unknown: set DOTNET_ENVIRONMENT (ASPNETCORE_ENVIRONMENT for the API) " +
                $"to one of {string.Join(", ", PlatformEnvironments.All)}");
        }

        foreach (var setting in settings.DistinctBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            var value = configuration[setting.Key];
            if (string.IsNullOrWhiteSpace(value))
            {
                if (setting.RequiredWhen?.Invoke(configuration) == true)
                {
                    problems.Add(
                        $"{setting.Key} is required (environment variable {setting.EnvironmentVariable}): {setting.Description}");
                }

                continue;
            }

            if (setting.AllowedValues is { } allowed && !allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                var shown = setting.Secret ? "the given value" : $"'{value}'";
                problems.Add($"{setting.Key} = {shown}: expected one of {string.Join(", ", allowed)}");
            }

            if (setting.Secret
                && PlatformEnvironments.IsKnown(environmentName)
                && !PlatformEnvironments.AllowsSecretsInSettingsFiles(environmentName)
                && FindSettingsFile(configuration, setting.Key) is { } file)
            {
                problems.Add(
                    $"{setting.Key} is a secret but comes from {file}: in {environmentName} give it as the environment " +
                    $"variable {setting.EnvironmentVariable} or a file in {PlatformConfigurationExtensions.SecretsDirectoryKey}");
            }
        }

        return problems;
    }

    /// <summary>Throws <see cref="InvalidConfigurationException"/> listing every problem of <see cref="Validate"/>.</summary>
    public static void EnsureValid(IConfiguration configuration, string environmentName, IEnumerable<Setting> settings)
    {
        var problems = Validate(configuration, environmentName, settings);
        if (problems.Count > 0)
        {
            throw new InvalidConfigurationException(problems);
        }
    }

    // The settings file that supplies the effective value of the key, if a file does: the last provider wins.
    private static string? FindSettingsFile(IConfiguration configuration, string key)
    {
        if (configuration is not IConfigurationRoot root)
        {
            return null;
        }

        var winner = root.Providers.LastOrDefault(p => p.TryGet(key, out _));
        return winner is FileConfigurationProvider file ? file.Source.Path ?? "a settings file" : null;
    }
}
