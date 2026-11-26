using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Platform.Http;
using Platform.Persistence;

namespace Platform.Modules.Reactions;

/// <summary>
/// The Reactions module in the API host (ADR-0061): likes on posts and comments, their API and their database. It needs
/// the Posts module (<c>IPostDirectory</c>) and the Comments module (<c>ICommentDirectory</c>) registered in the same
/// host.
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
        builder.Services.AddApiEndpoints<LikeEndpoints>();
        return builder;
    }
}
