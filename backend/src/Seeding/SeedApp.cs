using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Platform.Configuration;
using Platform.Persistence;

namespace Platform.Seeding;

/// <summary>
/// The seed command (<c>tools/Seed</c>, ADR-0009): applies the migrations of every module, then runs every module's
/// demo seeder. Options are configuration keys in the <c>--Key=value</c> form. It refuses to run in Staging and
/// Production unless <c>Seed:AllowProtectedEnvironment=true</c>.
/// </summary>
public static class SeedApp
{
    public const string ConnectionStringName = "Platform";
    public const string RandomSeedKey = "Seed:RandomSeed";
    public const string AllowProtectedEnvironmentKey = "Seed:AllowProtectedEnvironment";

    /// <summary>Exit code when the environment does not allow demo data.</summary>
    public const int RefusedExitCode = 3;

    /// <summary>The settings the command checks before it runs (ADR-0007); described in docs/CONFIG.md.</summary>
    public static IReadOnlyList<Setting> Settings { get; } =
    [
        new($"ConnectionStrings:{ConnectionStringName}",
            "PostgreSQL of the platform, Npgsql format: Host=...;Port=...;Database=...;Username=...;Password=...")
        {
            Secret = true,
            RequiredWhen = Setting.Always,
        },
        new(RandomSeedKey, "integer seed of the demo data; the same seed gives the same data")
        {
            Default = SeedData.DefaultSeed.ToString(CultureInfo.InvariantCulture),
        },
        new(AllowProtectedEnvironmentKey, "true lets the seeder write demo data in Staging or Production")
        {
            Default = "false",
            AllowedValues = ["true", "false"],
        },
    ];

    public const string Usage = "usage: seed [--Seed:RandomSeed=<int>] [--Seed:AllowProtectedEnvironment=true]";

    /// <param name="addModules">The module databases and demo seeders; the tool's catalog passes its own.</param>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Action<IServiceCollection, string> addModules)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(addModules);

        var words = args.TakeWhile(a => !a.StartsWith('-')).ToArray();
        if (words is not ([] or ["seed"]))
        {
            await error.WriteLineAsync(Usage);
            return 2;
        }

        // appsettings*.json are read next to the binary, whatever directory the command runs from.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args.Skip(words.Length).ToArray(),
            ContentRootPath = AppContext.BaseDirectory,
        });
        try
        {
            builder.ConfigurePlatform(Settings);
        }
        catch (InvalidConfigurationException e)
        {
            await error.WriteLineAsync(e.Message);
            return 2;
        }

        if (!int.TryParse(builder.Configuration[RandomSeedKey] ?? Settings[1].Default, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var seed))
        {
            await error.WriteLineAsync($"{RandomSeedKey} must be an integer.");
            return 2;
        }

        var environment = builder.Environment.EnvironmentName;
        if (environment is PlatformEnvironments.Staging or PlatformEnvironments.Production
            && !string.Equals(builder.Configuration[AllowProtectedEnvironmentKey], "true", StringComparison.Ordinal))
        {
            await error.WriteLineAsync(
                $"Demo data is not written in {environment}. Pass --{AllowProtectedEnvironmentKey}=true to do it anyway.");
            return RefusedExitCode;
        }

        addModules(builder.Services, builder.Configuration.GetConnectionString(ConnectionStringName)!);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ModuleMigrator>();
        using var host = builder.Build();

        try
        {
            await host.Services.GetRequiredService<ModuleMigrator>().MigrateAsync(TextWriter.Null);
            var data = new SeedData(seed, host.Services.GetRequiredService<TimeProvider>());
            await DemoSeedRunner.RunAsync(host.Services, data, output);
            return 0;
        }
#pragma warning disable CA1031 // The command reports any failure as a non-zero exit code.
        catch (Exception e)
#pragma warning restore CA1031
        {
            await error.WriteLineAsync($"seed failed: {e.Message}");
            return 1;
        }
    }
}

/// <summary>Runs every registered <see cref="IDemoSeeder"/>, each in its own scope; for the command and tests.</summary>
public static class DemoSeedRunner
{
    /// <returns>The number of rows added by all seeders.</returns>
    public static async Task<int> RunAsync(
        IServiceProvider services, SeedData data, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(output);

        await using var probe = services.CreateAsyncScope();
        var names = probe.ServiceProvider.GetServices<IDemoSeeder>().Select(s => s.Name).ToList();
        var total = 0;
        for (var i = 0; i < names.Count; i++)
        {
            await using var scope = services.CreateAsyncScope();
            var seeder = scope.ServiceProvider.GetServices<IDemoSeeder>().ElementAt(i);
            var added = await seeder.SeedAsync(data, cancellationToken);
            total += added;
            await output.WriteLineAsync($"{seeder.Name}: {added} added");
        }

        await output.WriteLineAsync(names.Count == 0
            ? "No demo seeders registered."
            : $"Seed {data.Seed}: {total} rows added.");
        return total;
    }
}
