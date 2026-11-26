using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Http;
using Platform.Messaging;
using Platform.Modules.Comments.Contracts;
using Platform.Persistence;

namespace Platform.Modules.Comments;

/// <summary>
/// The Comments module in the API host (ADR-0048): the comment tree, its API and its database. It needs the Posts
/// module (<c>IPostDirectory</c>) and the Reactions module (<c>ILikeCounts</c>, ADR-0062) registered in the same host,
/// and offers other modules <see cref="ICommentDirectory"/> (ADR-0061).
/// </summary>
public static class CommentsModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public static WebApplicationBuilder AddCommentsModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddModuleDbContext<CommentsDbContext>(
            builder.Configuration.GetConnectionString(ConnectionStringName)!);
        builder.Services.TryAddSingleton(CommentsSettings.ReadLimits(builder.Configuration));
        builder.Services.AddScoped<ICommentDirectory, CommentDirectory>();
        builder.Services.AddApiEndpoints<CommentEndpoints>();
        return builder;
    }

    /// <summary>
    /// The module's outbox in the API host (ADR-0005): a new comment publishes <c>CommentPosted</c> through
    /// <c>IModuleOutbox&lt;CommentsDbContext&gt;</c> (ADR-0092).
    /// </summary>
    public static IBusRegistrationConfigurator AddCommentsOutbox(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return bus.AddModuleOutbox<CommentsDbContext>();
    }
}
