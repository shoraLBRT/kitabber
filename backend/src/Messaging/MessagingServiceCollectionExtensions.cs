using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Configuration key of the transport (<see cref="MessagingTransport"/>).</summary>
    public const string TransportKey = "Messaging:Transport";

    /// <summary>Name of the RabbitMQ connection string: <c>amqp://user:password@host:port/vhost</c>.</summary>
    public const string RabbitMqConnectionName = "RabbitMq";

    /// <summary>
    /// Registers the message bus (MassTransit, ADR-0005). <paramref name="configure"/> adds consumers and module outboxes
    /// (<see cref="OutboxRegistrationExtensions.AddModuleOutbox{TContext}"/>). Endpoints are named in kebab-case after
    /// the consumer; queues on RabbitMQ are durable, so messages wait while no worker is running.
    /// </summary>
    public static IServiceCollection AddPlatformMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var transport = ReadTransport(configuration);
        var rabbitMq = transport == MessagingTransport.RabbitMq
            ? RabbitMqEndpoint.Parse(configuration.GetConnectionString(RabbitMqConnectionName))
            : null;

        // The host starts only when the bus is connected and every queue is bound: a message published right after
        // start is not dropped, and a host that cannot reach the broker fails to start instead of running idle.
        services.AddOptions<MassTransitHostOptions>().Configure(host =>
        {
            host.WaitUntilStarted = true;
            host.StartTimeout = TimeSpan.FromMinutes(1);
            host.StopTimeout = TimeSpan.FromSeconds(30);
        });
        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            configure?.Invoke(bus);
            if (rabbitMq is null)
            {
                bus.UsingInMemory((context, cfg) =>
                {
                    ConfigureRetries(cfg);
                    cfg.ConfigureEndpoints(context);
                });
                return;
            }

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitMq.Host, rabbitMq.Port, rabbitMq.VirtualHost, host =>
                {
                    host.Username(rabbitMq.Username);
                    host.Password(rabbitMq.Password);
                });
                ConfigureRetries(cfg);
                cfg.ConfigureEndpoints(context);
            });
        });
        return services;
    }

    private static MessagingTransport ReadTransport(IConfiguration configuration)
    {
        var value = configuration[TransportKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return MessagingTransport.RabbitMq;
        }

        return Enum.TryParse<MessagingTransport>(value, ignoreCase: true, out var transport) && Enum.IsDefined(transport)
            ? transport
            : throw new InvalidOperationException(
                $"{TransportKey} = '{value}': expected one of {string.Join(", ", Enum.GetNames<MessagingTransport>())}.");
    }

    // A failed message is retried in memory a few times, then goes to the endpoint's _error queue.
    private static void ConfigureRetries(IBusFactoryConfigurator cfg) =>
        cfg.UseMessageRetry(retry => retry.Intervals(100, 500, 1000));
}
