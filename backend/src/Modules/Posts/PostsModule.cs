using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Http;
using Platform.Messaging;
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
        builder.Services.AddScoped<IPostSearchSource, PostSearchSource>();
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
        builder.Services.AddScoped<HotnessCalculator>();
        builder.Services.AddHostedService<HotnessService>();
        builder.Services.AddApiEndpoints<PopularFeedEndpoints>();
        builder.Services.AddApiEndpoints<TopFeedEndpoints>();
        builder.Services.AddApiEndpoints<MyFeedEndpoints>();
        builder.Services.AddApiEndpoints<BookmarkFeedEndpoints>();
        return builder;
    }

    /// <summary>
    /// The module's outbox in the API host (ADR-0005): publishing, editing and deleting a post publish
    /// <see cref="PostChanged"/> through <c>IModuleOutbox&lt;PostsDbContext&gt;</c> (ADR-0094).
    /// </summary>
    public static IBusRegistrationConfigurator AddPostsOutbox(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return bus.AddModuleOutbox<PostsDbContext>();
    }

    /// <summary>
    /// What the workers host needs of the module: its database and <see cref="IPostSearchSource"/>, which the Search
    /// module reads (ADR-0094). The module has no consumers.
    /// </summary>
    public static IServiceCollection AddPostsWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddModuleDbContext<PostsDbContext>(configuration.GetConnectionString(ConnectionStringName)!);
        services.AddScoped<IPostSearchSource, PostSearchSource>();
        return services;
    }
}
