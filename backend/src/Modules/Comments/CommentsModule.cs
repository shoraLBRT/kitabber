using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Http;
using Platform.Persistence;

namespace Platform.Modules.Comments;

/// <summary>
/// The Comments module in the API host (ADR-0048): the comment tree, its API and its database. It needs the Posts
/// module (<c>IPostDirectory</c>) registered in the same host.
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
        builder.Services.AddApiEndpoints<CommentEndpoints>();
        return builder;
    }
}
