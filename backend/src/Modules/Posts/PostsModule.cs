using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Http;
using Platform.Modules.Posts.Contracts;
using Platform.Persistence;

namespace Platform.Modules.Posts;

/// <summary>
/// The Posts module in the API host (ADR-0029): the post API, the fresh feed (ADR-0043) with its cache (ADR-0080) and its database. It needs the Editor module
/// (<c>IBlockDocumentValidator</c>) and the Blogs module (<c>IBlogDirectory</c>) registered in the same host, and
/// offers other modules <see cref="IPostDirectory"/> (ADR-0048).
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
        builder.Services.AddScoped<IPostDirectory, PostDirectory>();
        builder.Services.AddApiEndpoints<PostEndpoints>();
        builder.Services.AddPlatformCache(builder.Configuration);
        builder.Services.AddScoped<FeedPageCache>();
        builder.Services.AddApiEndpoints<FreshFeedEndpoints>();
        builder.Services.AddApiEndpoints<BlogFeedEndpoints>();
        builder.Services.AddSingleton<ViewSaltCache>();
        builder.Services.AddScoped<IViewCounter, BufferedViewCounter>();
        builder.Services.AddSingleton<ViewFlusher>();
        builder.Services.AddHostedService<ViewFlushService>();
        builder.Services.AddApiEndpoints<PostViewEndpoints>();
        return builder;
    }
}
