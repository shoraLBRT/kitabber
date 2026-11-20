using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Caching;
using Platform.Http;
using StackExchange.Redis;

namespace Platform.Realtime;

/// <summary>Registers the realtime channel of ADR-0019 in the Api host.</summary>
public static class RealtimeServiceCollectionExtensions
{
    /// <summary>The address of the hub; the stand's proxy sends it to the API, WebSockets included.</summary>
    public const string HubPath = "/hubs/realtime";

    /// <summary>The authorization policy of the hub: an authenticated account, by ticket or by token.</summary>
    public const string HubPolicy = "realtime";

    /// <summary>The prefix of the backplane's Redis channels, beside the cache's <c>platform:</c> keys.</summary>
    public const string ChannelPrefix = "platform:realtime";

    /// <summary>
    /// SignalR with the Redis backplane when the platform cache is Redis (<c>Cache:Provider</c>), so an event
    /// published on one API instance reaches clients connected to any other; in memory otherwise (tests). Also the
    /// hub tickets with their endpoint and authentication scheme, the built-in topic policies and
    /// <see cref="IRealtimePublisher"/>.
    /// </summary>
    public static IServiceCollection AddPlatformRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPlatformCache(configuration);
        services.TryAddSingleton(TimeProvider.System);

        var signalR = services.AddSignalR();
        if (CacheServiceCollectionExtensions.ReadProvider(configuration) == CacheProvider.Redis)
        {
            signalR.AddStackExchangeRedis(CacheServiceCollectionExtensions.ReadRedisConnection(configuration), options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal(ChannelPrefix);
                // As the cache: the host starts while Redis is down and reconnects; readiness reports it.
                options.Configuration.AbortOnConnectFail = false;
            });
        }

        services.TryAddSingleton<RealtimeTicketStore>();
        services.TryAddSingleton<IRealtimePublisher, HubRealtimePublisher>();
        services.AddSingleton<IRealtimeTopicPolicy, AccountTopicPolicy>();
        services.AddSingleton<IRealtimeTopicPolicy, PostTopicPolicy>();
        services.AddApiEndpoints<RealtimeEndpoints>();
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, RealtimeTicketHandler>(RealtimeTicketHandler.SchemeName, null);
        services.AddAuthorizationBuilder().AddPolicy(HubPolicy, policy => policy
            .AddAuthenticationSchemes(RealtimeTicketHandler.SchemeName)
            .RequireAuthenticatedUser());
        return services;
    }

    /// <summary>Maps the hub at <see cref="HubPath"/>, for authenticated accounts only.</summary>
    public static WebApplication MapRealtime(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapHub<RealtimeHub>(HubPath, options =>
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling)
            .RequireAuthorization(HubPolicy)
            .ExcludeFromApi();
        return app;
    }
}
