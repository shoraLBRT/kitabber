using Microsoft.Extensions.DependencyInjection;

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
        // No modules yet: they arrive with their own tasks (ADR-0001).
    }
}
