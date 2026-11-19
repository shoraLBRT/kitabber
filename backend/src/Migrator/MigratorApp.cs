using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Persistence;

namespace Platform.Migrator;

/// <summary>
/// The migration command. Positional words choose the command, options are configuration keys in the
/// <c>--Key=value</c> form, for example <c>--ConnectionStrings:Platform=Host=...</c>.
/// </summary>
public static class MigratorApp
{
    public const string ConnectionStringName = "Platform";

    private const string Usage = "usage: migrate | status | rollback <schema>";

    /// <param name="addModules">The module databases to migrate; <see cref="ModuleCatalog"/> when omitted.</param>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Action<IServiceCollection, string>? addModules = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var words = args.TakeWhile(a => !a.StartsWith('-')).ToArray();
        var options = args.Skip(words.Length).ToArray();
        var command = words.FirstOrDefault() ?? "migrate";

        // appsettings*.json are read next to the binary, whatever directory the command runs from.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = options,
            ContentRootPath = AppContext.BaseDirectory,
        });
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await error.WriteLineAsync($"connection string '{ConnectionStringName}' is not set (ConnectionStrings__{ConnectionStringName})");
            return 2;
        }

        (addModules ?? ModuleCatalog.AddModuleDatabases)(builder.Services, connectionString);
        builder.Services.AddSingleton<ModuleMigrator>();
        using var host = builder.Build();

        try
        {
            var migrator = host.Services.GetRequiredService<ModuleMigrator>();
            switch (command, words.Length)
            {
                case ("migrate", 1) or ("migrate", 0):
                    await migrator.MigrateAsync(output);
                    return 0;
                case ("status", 1):
                    await migrator.StatusAsync(output);
                    return 0;
                case ("rollback", 2):
                    await migrator.RollbackLastAsync(words[1], output);
                    return 0;
                default:
                    await error.WriteLineAsync(Usage);
                    return 2;
            }
        }
#pragma warning disable CA1031 // The command reports any failure as a non-zero exit code.
        catch (Exception e)
#pragma warning restore CA1031
        {
            await error.WriteLineAsync($"{command} failed: {e.Message}");
            return 1;
        }
    }
}
