using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Platform.Seeding;

/// <summary>
/// The demo data of one module (ADR-0009). It writes only to its own module's database, builds its objects with the
/// module's factories, and is idempotent: a second run adds nothing, a run after a partial one adds what is missing.
/// Register it with <see cref="DemoSeederServiceCollectionExtensions.AddDemoSeeder{TSeeder}"/>.
/// </summary>
public interface IDemoSeeder
{
    /// <summary>The name in the seed report, usually the module schema.</summary>
    string Name { get; }

    /// <returns>The number of rows added.</returns>
    Task<int> SeedAsync(SeedData data, CancellationToken cancellationToken);
}

public static class DemoSeederServiceCollectionExtensions
{
    /// <summary>Adds a module's demo seeder; it is created in its own scope, so it can take the module context.</summary>
    public static IServiceCollection AddDemoSeeder<TSeeder>(this IServiceCollection services)
        where TSeeder : class, IDemoSeeder
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDemoSeeder, TSeeder>());
        return services;
    }
}

public static class SeedingDbContextExtensions
{
    /// <summary>
    /// Adds the <paramref name="rows"/> whose key is not in the table yet and saves them; the rows that are already
    /// there stay as they are. This is what makes a seeder idempotent.
    /// </summary>
    /// <returns>The number of rows added.</returns>
    public static async Task<int> AddMissingAsync<TEntity>(
        this DbContext db,
        IReadOnlyCollection<TEntity> rows,
        Expression<Func<TEntity, Guid>> key,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(key);
        var keyOf = key.Compile();
        var ids = rows.Select(keyOf).ToList();
        var existing = await db.Set<TEntity>().Select(key).Where(id => ids.Contains(id))
            .ToListAsync(cancellationToken);
        var known = existing.ToHashSet();
        var missing = rows.Where(r => !known.Contains(keyOf(r))).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        db.Set<TEntity>().AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }
}
