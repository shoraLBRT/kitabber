using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Messaging;
using Platform.Modules.Reactions.Contracts;
using Platform.Persistence;

namespace Platform.Modules.Reactions;

/// <summary>
/// The Reactions module in the API host (ADR-0061): reactions on posts and comments, their API and their database. It needs
/// the Posts module (<c>IPostDirectory</c>) and the Comments module (<c>ICommentDirectory</c>) registered in the same
/// host, and offers other modules <see cref="ILikeCounts"/> (ADR-0062) and <see cref="IReactionRatings"/> (ADR-0081)
/// and <see cref="IBookmarkList"/> (ADR-0116).
/// </summary>
public static class ReactionsModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public static WebApplicationBuilder AddReactionsModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddModuleDbContext<ReactionsDbContext>(
            builder.Configuration.GetConnectionString(ConnectionStringName)!);
        builder.Services.AddScoped<ILikeCounts, LikeCounts>();
        builder.Services.AddScoped<IReactionRatings, ReactionRatings>();
        builder.Services.AddScoped<IBookmarkList, BookmarkList>();
        builder.Services.AddApiEndpoints<ReactionEndpoints>();
        builder.Services.AddApiEndpoints<ReactionSetEndpoints>();
        builder.Services.AddApiEndpoints<BookmarkEndpoints>();
        return builder;
    }

    /// <summary>
    /// The module's outbox in the API host (ADR-0005): a new reaction publishes <c>ReactionPut</c> through
    /// <c>IModuleOutbox&lt;ReactionsDbContext&gt;</c> (ADR-0092).
    /// </summary>
    public static IBusRegistrationConfigurator AddReactionsOutbox(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return bus.AddModuleOutbox<ReactionsDbContext>();
    }
}
