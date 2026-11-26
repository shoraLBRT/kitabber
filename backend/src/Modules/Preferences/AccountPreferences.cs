namespace Platform.Modules.Preferences;

/// <summary>The colour theme of the interface (FR-UI): the system's choice, light or dark.</summary>
public enum ThemeChoice
{
    System = 1,
    Light = 2,
    Dark = 3,
}

/// <summary>
/// The settings of one person that follow them to every device (ADR-0070). An account with no row has the defaults
/// of <see cref="For"/>. A later section (the feeds, E08-13; the notifications, E12-05) adds its columns here or keeps
/// them in the module that uses them, as its own ADR decides.
/// </summary>
public sealed class AccountPreferences
{
    public Guid AccountId { get; init; }

    /// <summary>The theme of «Основные»; the left menu's switch changes the same choice.</summary>
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>What an account has before it changes anything.</summary>
    public static AccountPreferences For(Guid accountId) =>
        accountId == Guid.Empty
            ? throw new ArgumentException("Settings belong to an account.", nameof(accountId))
            : new() { AccountId = accountId };
}

/// <summary>The rules of the settings, without a database.</summary>
public static class PreferenceRules
{
    /// <summary>The theme for its API name (case-insensitive), or <c>null</c> for anything else.</summary>
    public static ThemeChoice? ParseTheme(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "system" => ThemeChoice.System,
        "light" => ThemeChoice.Light,
        "dark" => ThemeChoice.Dark,
        _ => null,
    };

    /// <summary>The API name of a theme.</summary>
    public static string NameOf(ThemeChoice theme) => theme switch
    {
        ThemeChoice.Light => "light",
        ThemeChoice.Dark => "dark",
        _ => "system",
    };
}
