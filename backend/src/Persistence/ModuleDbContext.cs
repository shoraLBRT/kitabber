using Microsoft.EntityFrameworkCore;

namespace Platform.Persistence;

/// <summary>
/// Base class for a module's <see cref="DbContext"/>: puts every table of the module into the module's schema.
/// Register it with <see cref="ModuleDatabaseServiceCollectionExtensions.AddModuleDbContext{TContext}"/>.
/// </summary>
public abstract class ModuleDbContext<TContext>(DbContextOptions<TContext> options) : DbContext(options)
    where TContext : ModuleDbContext<TContext>, IModuleDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(TContext.Schema);
        base.OnModelCreating(modelBuilder);
    }
}
