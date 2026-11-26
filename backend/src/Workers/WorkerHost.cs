using MassTransit;
using Platform.Caching;
using Platform.Configuration;
using Platform.Mail;
using Platform.Messaging;
using Platform.Modules.Blogs;
using Platform.Observability;

namespace Platform.Workers;

/// <summary>
/// Composition of the background workers host: the bus with every module's consumers, the cache and the delivery of
/// queued mail (ADR-0017). A module task adds its services to <see cref="CreateBuilder"/> and its consumers and outbox
/// in <see cref="AddModules"/>; tests build the host without modules through <see cref="Configure"/>, with their own
/// consumers.
/// </summary>
public static class WorkerHost
{
    /// <summary>The settings the host checks at start (ADR-0007); each one is described in docs/CONFIG.md.</summary>
    /// <summary>The service name in traces, metrics and logs (ADR-0015).</summary>
    public const string ServiceName = "platform-workers";

    public static IReadOnlyList<Setting> Settings { get; } = [.. MessagingSettings.All, .. CacheSettings.All, .. MailSettings.All, .. ObservabilitySettings.All, .. BlogsModule.WorkerSettings];

    public static HostApplicationBuilder CreateBuilder(string[] args, Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.ConfigurePlatform(Settings);
        builder.AddPlatformObservability(ServiceName);
        builder.Services.AddBlogsWorkers(builder.Configuration);
        Configure(builder.Services, builder.Configuration, bus =>
        {
            AddModules(bus);
            configureBus?.Invoke(bus);
        });
        return builder;
    }

    public static void Configure(
        IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddPlatformCache(configuration);
        services.AddPlatformMailDelivery(configuration);
        services.AddPlatformMessaging(configuration, bus =>
        {
            bus.AddMailDelivery();
            configureBus?.Invoke(bus);
        });
    }

    // One line per module: its consumers and, when it publishes, its outbox (AddModuleOutbox, ADR-0005).
    private static void AddModules(IBusRegistrationConfigurator bus) => bus.AddBlogsConsumers();
}
