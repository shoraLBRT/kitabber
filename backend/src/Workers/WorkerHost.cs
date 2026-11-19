using MassTransit;
using Platform.Caching;
using Platform.Messaging;

namespace Platform.Workers;

/// <summary>
/// Composition of the background workers host: the bus with every module's consumers and the cache. A module task
/// adds its consumers and outbox in <see cref="AddModules"/>; tests build the same host with their own consumers.
/// </summary>
public static class WorkerHost
{
    public static HostApplicationBuilder CreateBuilder(string[] args, Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        var builder = Host.CreateApplicationBuilder(args);
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
