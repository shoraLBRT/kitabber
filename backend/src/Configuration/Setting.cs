using Microsoft.Extensions.Configuration;

namespace Platform.Configuration;

/// <summary>
/// One configuration setting of the platform (ADR-0007). The library that reads a setting declares it; a host lists
/// the settings of the libraries it uses, and <see cref="PlatformConfigurationExtensions.ConfigurePlatform"/> checks
/// them before the host is built. Every declared setting is described in <c>docs/CONFIG.md</c> (a test checks it).
/// </summary>
/// <param name="Key">Configuration key with <c>:</c> between sections, for example <c>ConnectionStrings:Redis</c>.</param>
/// <param name="Description">What the setting is for and its format; shown when it is missing.</param>
public sealed record Setting(string Key, string Description)
{
    /// <summary>
    /// A password, key or connection string with credentials. Outside Development and Test a secret may come only
    /// from environment variables, secret files or the command line, never from a committed <c>appsettings*.json</c>.
    /// </summary>
    public bool Secret { get; init; }

    /// <summary>The value used when the setting is not given; documentation only, the reader applies it.</summary>
    public string? Default { get; init; }

    /// <summary>When set, the value must be one of these (case-insensitive).</summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }

    /// <summary>When the setting must be given: <see cref="Always"/>, a condition on other settings, or null if optional.</summary>
    public Func<IConfiguration, bool>? RequiredWhen { get; init; }

    /// <summary>For <see cref="RequiredWhen"/>: the setting is required in every configuration.</summary>
    public static bool Always(IConfiguration configuration) => true;

    /// <summary>The name of the environment variable that sets the key: <c>:</c> becomes <c>__</c>.</summary>
    public string EnvironmentVariable => Key.Replace(":", "__", StringComparison.Ordinal);
}
