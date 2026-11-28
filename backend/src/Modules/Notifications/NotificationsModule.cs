using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Messaging;
using Platform.Persistence;
using Platform.Realtime;

namespace Platform.Modules.Notifications;

/// <summary>
/// The Notifications module in the workers host (ADR-0092): its consumers turn the events of Comments, Reactions and
/// Blogs into notifications and keep them in the schema <c>notifications</c>. In the API host it gives the feed and the
/// counter of the unread ones (E12-02, ADR-0093) and tells the reader's open pages that it changed (E12-04, ADR-0123);
/// the delivery by push and by mail comes with E12-06 and E12-07.
/// </summary>
public static class NotificationsModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    /// <summary>The module's database and its read endpoints in the API host (E12-02, ADR-0093).</summary>
    public static WebApplicationBuilder AddNotificationsModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddModuleDbContext<NotificationsDbContext>(
            builder.Configuration.GetConnectionString(ConnectionStringName)!);
        builder.Services.AddApiEndpoints<NotificationEndpoints>();
        return builder;
    }

    /// <summary>
    /// The module's database and services in the workers host; its consumers come from
    /// <see cref="AddNotificationsConsumers"/>. The connection string is the one the Blogs module already checks there.
    /// </summary>
    public static IServiceCollection AddNotificationsWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddModuleDbContext<NotificationsDbContext>(configuration.GetConnectionString(ConnectionStringName)!);
        services.AddScoped<NotificationWriter>();
        services.AddPlatformRealtimePublisher(configuration);
        return services;
    }

    /// <summary>
    /// The module's consumers with the inbox in its schema (ADR-0005). A consumer that adds notifications publishes
    /// <see cref="NotificationsArrived"/> through its own outbox, and <see cref="NotificationsArrivedRealtimeConsumer"/>
    /// tells the recipients' pages once that is committed (ADR-0123).
    /// </summary>
    public static IBusRegistrationConfigurator AddNotificationsConsumers(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.AddConsumer<CommentPostedNotificationsConsumer, CommentPostedNotificationsConsumerDefinition>();
        bus.AddConsumer<ReactionPutNotificationsConsumer, ReactionPutNotificationsConsumerDefinition>();
        bus.AddConsumer<BlogSubscribedNotificationsConsumer, BlogSubscribedNotificationsConsumerDefinition>();
        bus.AddConsumer<PostRepostedNotificationsConsumer, PostRepostedNotificationsConsumerDefinition>();
        bus.AddConsumer<PostPublishedNotificationsConsumer, PostPublishedNotificationsConsumerDefinition>();
        bus.AddConsumer<TopicTransferOfferedNotificationsConsumer, TopicTransferOfferedNotificationsConsumerDefinition>();
        bus.AddConsumer<AccountDeletedNotificationsConsumer, AccountDeletedNotificationsConsumerDefinition>();
        bus.AddConsumer<NotificationsArrivedRealtimeConsumer>();
        bus.AddEntityFrameworkOutbox<NotificationsDbContext>(outbox =>
        {
            outbox.UseModulePostgres();
            outbox.DuplicateDetectionWindow = TimeSpan.FromDays(1);
        });
        return bus;
    }
}
