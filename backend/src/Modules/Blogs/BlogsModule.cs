using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Configuration;
using Platform.Http;
using Platform.Messaging;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Blogs.Profiles;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Modules.Blogs.Topics;
using Platform.Persistence;

namespace Platform.Modules.Blogs;

/// <summary>
/// The Blogs module in the hosts (ADR-0025): the profile API in the API host, the consumer of Identity events in the
/// workers.
/// </summary>
public static class BlogsModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public const string DatabaseKey = "ConnectionStrings:" + ConnectionStringName;

    /// <summary>The settings the workers check at start for this module; each one is described in docs/CONFIG.md.</summary>
    public static IReadOnlyList<Setting> WorkerSettings { get; } =
    [
        new(DatabaseKey, "PostgreSQL of the platform: Host=...;Database=...;Username=...;Password=...")
        {
            Secret = true,
            RequiredWhen = Setting.Always,
        },
    ];

    /// <summary>The profile API (<see cref="ProfileEndpoints"/>) and its database, in the API host.</summary>
    public static WebApplicationBuilder AddBlogsModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddServices(builder.Services, builder.Configuration);
        builder.Services.AddApiEndpoints<ProfileEndpoints>();
        builder.Services.TryAddSingleton(BlogsSettings.ReadLimits(builder.Configuration));
        builder.Services.AddApiEndpoints<TopicEndpoints>();
        builder.Services.AddApiEndpoints<TopicCatalogEndpoints>();
        builder.Services.AddApiEndpoints<SubscriptionEndpoints>();
        builder.Services.AddApiEndpoints<SubscriptionListEndpoints>();
        return builder;
    }

    /// <summary>The module's database and services in the workers host; the consumers come from <see cref="AddBlogsConsumers"/>.</summary>
    public static IServiceCollection AddBlogsWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        AddServices(services, configuration);
        return services;
    }

    /// <summary>
    /// The module's outbox in the API host (ADR-0005): the subscription endpoints publish <c>BlogSubscribed</c> through
    /// <c>IModuleOutbox&lt;BlogsDbContext&gt;</c> (ADR-0092).
    /// </summary>
    public static IBusRegistrationConfigurator AddBlogsOutbox(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return bus.AddModuleOutbox<BlogsDbContext>();
    }

    /// <summary>The module's consumers with the inbox in its schema (ADR-0005), in the workers host.</summary>
    public static IBusRegistrationConfigurator AddBlogsConsumers(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.AddConsumer<AccountCreatedBlogsConsumer, AccountCreatedBlogsConsumerDefinition>();
        // Drains the queue of the former AccountActivated for one release (ADR-0087, item 9).
        bus.AddConsumer<AccountActivatedConsumer, AccountActivatedConsumerDefinition>();
        bus.AddEntityFrameworkOutbox<BlogsDbContext>(outbox =>
        {
            outbox.UseModulePostgres();
            outbox.DuplicateDetectionWindow = TimeSpan.FromDays(1);
        });
        return bus;
    }

    private static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<BlogsDbContext>(configuration.GetConnectionString(ConnectionStringName)!);
        services.AddScoped<PersonalBlogs>();
        services.AddScoped<IBlogDirectory, BlogDirectory>();
        services.AddScoped<IBlogSubscriptions, BlogSubscriptions>();
        services.AddScoped<IBlogSearchSource, BlogSearchSource>();
    }
}
