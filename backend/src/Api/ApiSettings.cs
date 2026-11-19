using Platform.Configuration;

namespace Platform.Api;

/// <summary>
/// The settings the API host checks at start (ADR-0007): those of every library the host registers. A module adds
/// its settings here together with its registration; each one is described in docs/CONFIG.md.
/// </summary>
public static class ApiSettings
{
    public static IReadOnlyList<Setting> All { get; } = [];
}
