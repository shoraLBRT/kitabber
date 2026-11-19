namespace Platform.Messaging;

/// <summary>Which transport the bus uses: <c>Messaging:Transport</c> in configuration.</summary>
public enum MessagingTransport
{
    /// <summary>RabbitMQ from <c>ConnectionStrings:RabbitMq</c>; the default for every environment.</summary>
    RabbitMq,

    /// <summary>In-process transport for tests: no broker, messages are lost when the process stops.</summary>
    InMemory,
}
