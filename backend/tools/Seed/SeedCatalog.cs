using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs;
using Platform.Modules.Identity;
using Platform.Persistence;
using Platform.Seeding;

namespace Platform.Seed;

/// <summary>
/// Every module the seeder fills (ADR-0009). A module task adds its database, as in the migrator's catalog, and its
/// demo seeder:
/// <c>services.AddModuleDbContext&lt;IdentityDbContext&gt;(connectionString).AddDemoSeeder&lt;IdentityDemoSeeder&gt;();</c>
/// </summary>
public static class SeedCatalog
{
    public static void AddModules(IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddModuleDbContext<IdentityDbContext>(connectionString).AddDemoSeeder<IdentityDemoSeeder>();
        services.AddModuleDbContext<BlogsDbContext>(connectionString).AddDemoSeeder<BlogsDemoSeeder>();
    }
}
