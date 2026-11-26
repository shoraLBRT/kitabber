using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Messaging;
using Platform.Persistence;

namespace Platform.Modules.Notifications;

/// <summary>
/// The Notifications module in the workers host (ADR-0092): its consumers turn the events of Comments, Reactions and
/// Blogs into notifications and keep them in the schema <c>notifications</c>. The feed and the counter of the API come
/// with E12-02; the delivery in real time, by push and by mail with E12-04, E12-06 and E12-07.
/// </summary>
public static class NotificationsModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

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
        return services;
    }

    /// <summary>The module's consumers with the inbox in its schema (ADR-0005); the module publishes nothing.</summary>
    public static IBusRegistrationConfigurator AddNotificationsConsumers(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.AddConsumer<CommentPostedNotificationsConsumer, CommentPostedNotificationsConsumerDefinition>();
        bus.AddConsumer<ReactionPutNotificationsConsumer, ReactionPutNotificationsConsumerDefinition>();
        bus.AddConsumer<BlogSubscribedNotificationsConsumer, BlogSubscribedNotificationsConsumerDefinition>();
        bus.AddConsumer<AccountDeletedNotificationsConsumer, AccountDeletedNotificationsConsumerDefinition>();
        bus.AddEntityFrameworkOutbox<NotificationsDbContext>(outbox =>
        {
            outbox.UseModulePostgres();
            outbox.DuplicateDetectionWindow = TimeSpan.FromDays(1);
        });
        return bus;
    }
}
