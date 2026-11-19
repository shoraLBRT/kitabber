using Platform.Configuration;

namespace Platform.Messaging;

/// <summary>The settings <see cref="MessagingServiceCollectionExtensions.AddPlatformMessaging"/> reads (docs/CONFIG.md).</summary>
public static class MessagingSettings
{
    public static IReadOnlyList<Setting> All { get; } =
    [
        new(MessagingServiceCollectionExtensions.TransportKey, "message transport: RabbitMq, or InMemory for tests")
        {
            Default = nameof(MessagingTransport.RabbitMq),
            AllowedValues = Enum.GetNames<MessagingTransport>(),
        },
        new($"ConnectionStrings:{MessagingServiceCollectionExtensions.RabbitMqConnectionName}",
            "RabbitMQ broker: amqp://user:password@host:port/vhost")
        {
            Secret = true,
            RequiredWhen = configuration => !string.Equals(
                configuration[MessagingServiceCollectionExtensions.TransportKey],
                nameof(MessagingTransport.InMemory),
                StringComparison.OrdinalIgnoreCase),
        },
    ];
}
