using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Persistence;

namespace Platform.Modules.Posts;

/// <summary>
/// The Posts module in the API host (ADR-0029): the post API, the fresh feed (ADR-0043) and its database. It needs the Editor module
/// (<c>IBlockDocumentValidator</c>) and the Blogs module (<c>IBlogDirectory</c>) registered in the same host.
/// </summary>
public static class PostsModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public static WebApplicationBuilder AddPostsModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddModuleDbContext<PostsDbContext>(
            builder.Configuration.GetConnectionString(ConnectionStringName)!);
        builder.Services.AddApiEndpoints<PostEndpoints>();
        builder.Services.AddApiEndpoints<FreshFeedEndpoints>();
        return builder;
    }
}
