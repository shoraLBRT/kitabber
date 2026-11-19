using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Persistence;

/// <summary>Applies and rolls back the migrations of every registered module database.</summary>
public sealed class ModuleMigrator
{
    private readonly IServiceProvider _services;

    public ModuleMigrator(IServiceProvider services, IEnumerable<ModuleDatabase> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _services = services;
        Modules = [.. modules.OrderBy(m => m.Schema, StringComparer.Ordinal)];
        var duplicate = Modules.GroupBy(m => m.Schema, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Schema '{duplicate.Key}' is claimed by {string.Join(", ", duplicate.Select(m => m.ContextType.Name))}; " +
                "every module owns its own schema.");
        }
    }

    public IReadOnlyList<ModuleDatabase> Modules { get; }

    /// <summary>Applies all pending migrations of every module, module by module.</summary>
    public async Task MigrateAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var module in Modules)
        {
            await using var scope = _services.CreateAsyncScope();
            var db = Resolve(scope, module);
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            await db.Database.MigrateAsync(cancellationToken);
            await output.WriteLineAsync($"{module.Schema}: applied {pending} migration(s)");
        }
    }

    /// <summary>Rolls back the last applied migration of one module. Returns the migration rolled back, if any.</summary>
    public async Task<string?> RollbackLastAsync(string schema, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        var module = Modules.FirstOrDefault(m => m.Schema == schema)
            ?? throw new ArgumentException($"Unknown module schema '{schema}'.", nameof(schema));
        await using var scope = _services.CreateAsyncScope();
        var db = Resolve(scope, module);
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        if (applied.Count == 0)
        {
            await output.WriteLineAsync($"{schema}: nothing to roll back");
            return null;
        }

        var target = applied.Count > 1 ? applied[^2] : Migration.InitialDatabase;
        await db.GetService<IMigrator>().MigrateAsync(target, cancellationToken: cancellationToken);
        await output.WriteLineAsync($"{schema}: rolled back {applied[^1]}");
        return applied[^1];
    }

    /// <summary>Prints applied and pending migrations of every module.</summary>
    public async Task StatusAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var module in Modules)
        {
            await using var scope = _services.CreateAsyncScope();
            var db = Resolve(scope, module);
            var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Count();
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            await output.WriteLineAsync($"{module.Schema}: {applied} applied, {pending} pending");
        }
    }

    private static DbContext Resolve(AsyncServiceScope scope, ModuleDatabase module) =>
        (DbContext)scope.ServiceProvider.GetRequiredService(module.ContextType);
}
