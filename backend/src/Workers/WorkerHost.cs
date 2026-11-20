using MassTransit;
using Platform.Caching;
using Platform.Configuration;
using Platform.Messaging;
using Platform.Observability;

namespace Platform.Workers;

/// <summary>
/// Composition of the background workers host: the bus with every module's consumers and the cache. A module task
/// adds its consumers and outbox in <see cref="AddModules"/>; tests build the same host with their own consumers.
/// </summary>
public static class WorkerHost
{
    /// <summary>The settings the host checks at start (ADR-0007); each one is described in docs/CONFIG.md.</summary>
    /// <summary>The service name in traces, metrics and logs (ADR-0015).</summary>
    public const string ServiceName = "platform-workers";

    public static IReadOnlyList<Setting> Settings { get; } = [.. MessagingSettings.All, .. CacheSettings.All, .. ObservabilitySettings.All];

    public static HostApplicationBuilder CreateBuilder(string[] args, Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.ConfigurePlatform(Settings);
        builder.AddPlatformObservability(ServiceName);
        Configure(builder.Services, builder.Configuration, configureBus);
        return builder;
    }

    public static void Configure(
        IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddPlatformCache(configuration);
        services.AddPlatformMessaging(configuration, bus =>
        {
            AddModules(bus);
            configureBus?.Invoke(bus);
        });
    }

    // One line per module: bus.AddConsumers(typeof(SomeModule).Assembly) and bus.AddModuleOutbox<SomeDbContext>().
    private static void AddModules(IBusRegistrationConfigurator bus) => _ = bus;
}
