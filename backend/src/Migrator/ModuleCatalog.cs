using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs;
using Platform.Modules.Comments;
using Platform.Modules.Identity;
using Platform.Modules.Media;
using Platform.Modules.Posts;
using Platform.Modules.Preferences;
using Platform.Modules.Reactions;
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
        services.AddModuleDbContext<PostsDbContext>(connectionString);
        services.AddModuleDbContext<CommentsDbContext>(connectionString);
        services.AddModuleDbContext<MediaDbContext>(connectionString);
        services.AddModuleDbContext<ReactionsDbContext>(connectionString);
        services.AddModuleDbContext<PreferencesDbContext>(connectionString);
    }
}
