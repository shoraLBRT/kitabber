using System.Text.Json;
using Platform.Api;
using Platform.Migrator;
using Platform.Seeding;
using Platform.Workers;

namespace Platform.Configuration.Tests;

/// <summary>
/// The declared settings against the repository: each one is described in docs/CONFIG.md, and committed settings
/// files of Staging and Production (and the base appsettings.json) hold no secrets.
/// </summary>
public sealed class SettingsCatalogTests
{
    public static IReadOnlyList<Setting> AllSettings { get; } =
    [
        .. PlatformConfigurationExtensions.HostSettings,
        .. ApiSettings.All,
        .. WorkerHost.Settings,
        .. MigratorApp.Settings,
        .. SeedApp.Settings,
    ];

    private static readonly string _root = FindRoot();

    [Fact]
    public void EverySettingIsDescribedInConfigDoc()
    {
        var doc = File.ReadAllText(Path.Combine(_root, "docs", "CONFIG.md"));

        var undocumented = AllSettings
            .Where(s => !doc.Contains($"`{s.Key}`", StringComparison.Ordinal)
                || !doc.Contains($"`{s.EnvironmentVariable}`", StringComparison.Ordinal))
            .Select(s => s.Key)
            .Distinct()
            .ToList();

        Assert.True(undocumented.Count == 0, $"docs/CONFIG.md does not describe: {string.Join(", ", undocumented)}");
    }

    [Fact]
    public void CommittedSettingsFilesOfStandsHoldNoSecrets()
    {
        var secrets = AllSettings.Where(s => s.Secret).Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        foreach (var file in SettingsFiles())
        {
            var environment = EnvironmentOf(file);
            if (environment is not null && PlatformEnvironments.AllowsSecretsInSettingsFiles(environment))
            {
                continue;
            }

            using var json = JsonDocument.Parse(File.ReadAllText(file));
            found.AddRange(Flatten(json.RootElement, prefix: null)
                .Where(secrets.Contains)
                .Select(key => $"{Path.GetRelativePath(_root, file)}: {key}"));
        }

        Assert.True(found.Count == 0, $"secrets in committed settings files: {string.Join("; ", found)}");
    }

    [Fact]
    public void SettingsFilesAreNamedAfterKnownEnvironments()
    {
        var unknown = SettingsFiles()
            .Where(f => EnvironmentOf(f) is { } e && !PlatformEnvironments.IsKnown(e))
            .Select(f => Path.GetRelativePath(_root, f))
            .ToList();

        Assert.Empty(unknown);
    }

    private static IEnumerable<string> SettingsFiles() =>
        Directory.EnumerateFiles(Path.Combine(_root, "backend", "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "bin" or "obj"));

    // appsettings.Production.json → Production; appsettings.json → null (every environment reads it).
    private static string? EnvironmentOf(string file)
    {
        var parts = Path.GetFileNameWithoutExtension(file).Split('.', 2);
        return parts.Length == 2 ? parts[1] : null;
    }

    private static IEnumerable<string> Flatten(JsonElement element, string? prefix)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            if (prefix is not null)
            {
                yield return prefix;
            }

            yield break;
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var key in Flatten(property.Value, prefix is null ? property.Name : $"{prefix}:{property.Name}"))
            {
                yield return key;
            }
        }
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "backend", "Platform.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("repository root (backend/Platform.slnx) not found");
    }
}
