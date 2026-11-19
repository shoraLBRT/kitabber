namespace Platform.Http;

/// <summary>
/// API versions. The version is the first path segment after <c>/api</c> (ADR-0006): <c>/api/v1/...</c>.
/// Only a breaking change opens a new version; additive changes stay in the current one.
/// </summary>
public static class ApiVersions
{
    /// <summary>The current and only version.</summary>
    public const string V1 = "v1";

    /// <summary>The route prefix of a version, for example <c>/api/v1</c>.</summary>
    public static string Prefix(string version) => $"/api/{version}";
}
