using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Messaging;
using Platform.Persistence;

namespace Platform.Modules.Preferences;

/// <summary>
/// The Preferences module in the API host (ADR-0070): the settings of a person kept on the server, so that every
/// device applies the same choice, their API and their database. In the workers host it erases the settings of a
/// deleted account (ADR-0087).
/// </summary>
public static class PreferencesModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public static WebApplicationBuilder AddPreferencesModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddDatabase(builder.Services, builder.Configuration);
        builder.Services.AddApiEndpoints<PreferenceEndpoints>();
        return builder;
    }

    /// <summary>
    /// The module's database in the workers host; its consumers come from <see cref="AddPreferencesConsumers"/>. The
    /// connection string is the one the Blogs module already checks there.
    /// </summary>
    public static IServiceCollection AddPreferencesWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        AddDatabase(services, configuration);
        return services;
    }

    /// <summary>The module's consumers with the inbox in its schema (ADR-0005); the module publishes nothing.</summary>
    public static IBusRegistrationConfigurator AddPreferencesConsumers(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.AddConsumer<AccountDeletedConsumer, AccountDeletedConsumerDefinition>();
        bus.AddEntityFrameworkOutbox<PreferencesDbContext>(outbox =>
        {
            outbox.UseModulePostgres();
            outbox.DuplicateDetectionWindow = TimeSpan.FromDays(1);
        });
        return bus;
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<PreferencesDbContext>(configuration.GetConnectionString(ConnectionStringName)!);
}
