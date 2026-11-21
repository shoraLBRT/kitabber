using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs;
using Platform.Modules.Identity;
using Platform.Persistence;

namespace Platform.Migrator;

/// <summary>
/// Every module database the migrator applies. A module task adds its line here:
/// <c>services.AddModuleDbContext&lt;IdentityDbContext&gt;(connectionString);</c>
/// </summary>
public static class ModuleCatalog
{
    public static void AddModuleDatabases(IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddModuleDbContext<IdentityDbContext>(connectionString);
        services.AddModuleDbContext<BlogsDbContext>(connectionString);
    }
}
