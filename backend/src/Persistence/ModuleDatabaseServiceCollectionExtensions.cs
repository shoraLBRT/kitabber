using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Persistence;

public static partial class ModuleDatabaseServiceCollectionExtensions
{
    /// <summary>The tag of readiness checks, the same in every library and in the Api's /health/ready.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Registers a module's context on PostgreSQL: snake_case names, the module schema, and the migration history
    /// table in that schema. Migrations live in the context's assembly.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string connectionString)
        where TContext : ModuleDbContext<TContext>, IModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var schema = TContext.Schema;
        if (!SchemaName().IsMatch(schema))
        {
            throw new InvalidOperationException(
                $"{typeof(TContext).Name}: schema '{schema}' must be the module name in snake_case.");
        }

        services.AddDbContext<TContext>(options => UseModuleDatabase<TContext>(options, connectionString));
        services.AddSingleton(new ModuleDatabase(typeof(TContext), schema));

        // Readiness (ADR-0015): the host is not ready while the module's database does not answer.
        services.AddHealthChecks().AddDbContextCheck<TContext>($"database:{schema}", tags: [ReadyTag]);
        return services;
    }

    /// <summary>The options every module context uses; also for design-time factories and tests.</summary>
    public static DbContextOptionsBuilder UseModuleDatabase<TContext>(DbContextOptionsBuilder options, string connectionString)
        where TContext : ModuleDbContext<TContext>, IModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        return options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(ModuleDatabase.HistoryTable, TContext.Schema)
                .MigrationsAssembly(typeof(TContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention();
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SchemaName();
}
