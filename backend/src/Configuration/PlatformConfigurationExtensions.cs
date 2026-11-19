using Microsoft.Extensions.Hosting;

namespace Platform.Configuration;

public static class PlatformConfigurationExtensions
{
    /// <summary>Configuration key of the secret files directory (<see cref="SecretFilesConfigurationSource"/>).</summary>
    public const string SecretsDirectoryKey = "Secrets:Directory";

    /// <summary>The settings every host reads itself, whatever libraries it uses.</summary>
    public static IReadOnlyList<Setting> HostSettings { get; } =
    [
        new(SecretsDirectoryKey, "directory with one file per secret setting, named like the environment variable " +
            "(ConnectionStrings__Redis); for Docker and Kubernetes secrets"),
    ];

    /// <summary>
    /// Finishes the configuration of a host (ADR-0007) before it is built: adds the secret files from
    /// <c>Secrets:Directory</c> on top of the standard layers (<c>appsettings.json</c>,
    /// <c>appsettings.{Environment}.json</c>, user secrets in Development, environment variables, command line), then
    /// checks the environment name and <paramref name="settings"/>. A host with a missing or wrong setting does not
    /// start: <see cref="InvalidConfigurationException"/> names every problem.
    /// </summary>
    public static TBuilder ConfigurePlatform<TBuilder>(this TBuilder builder, IEnumerable<Setting> settings)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);
        var directory = builder.Configuration[SecretsDirectoryKey];
        if (!string.IsNullOrWhiteSpace(directory))
        {
            builder.Configuration.Add(new SecretFilesConfigurationSource(directory));
        }

        ConfigurationValidator.EnsureValid(
            builder.Configuration,
            builder.Environment.EnvironmentName,
            HostSettings.Concat(settings));
        return builder;
    }
}
