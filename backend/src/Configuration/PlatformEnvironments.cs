namespace Platform.Configuration;

/// <summary>
/// The environments a host may run in (ADR-0007). Each adds its own <c>appsettings.{Environment}.json</c> layer; any
/// other name stops the host, so a typo never silently runs with Production defaults.
/// </summary>
public static class PlatformEnvironments
{
    /// <summary>A developer machine with the local compose environment.</summary>
    public const string Development = "Development";

    /// <summary>Automated tests: fakes and in-memory transports.</summary>
    public const string Test = "Test";

    /// <summary>The stage stand: production-like, its own secrets.</summary>
    public const string Staging = "Staging";

    /// <summary>The production stand.</summary>
    public const string Production = "Production";

    public static IReadOnlyList<string> All { get; } = [Development, Test, Staging, Production];

    public static bool IsKnown(string? name) => All.Contains(name, StringComparer.Ordinal);

    /// <summary>
    /// Whether committed settings files may hold secrets: only local, throw-away values of Development and Test.
    /// </summary>
    public static bool AllowsSecretsInSettingsFiles(string name) =>
        name is Development or Test;
}
