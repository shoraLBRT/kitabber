namespace Platform.Messaging;

/// <summary>The broker address taken apart from an <c>amqp://user:password@host:port/vhost</c> connection string.</summary>
public sealed record RabbitMqEndpoint(string Host, ushort Port, string VirtualHost, string Username, string Password)
{
    public const ushort DefaultPort = 5672;

    public static RabbitMqEndpoint Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{MessagingServiceCollectionExtensions.RabbitMqConnectionName} is not set: " +
                $"give amqp://user:password@host:port/ or set {MessagingServiceCollectionExtensions.TransportKey} = InMemory.");
        }

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) || uri.Scheme != "amqp")
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{MessagingServiceCollectionExtensions.RabbitMqConnectionName} must be an amqp:// URI.");
        }

        var user = uri.UserInfo.Split(':', 2);
        var vhost = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        return new RabbitMqEndpoint(
            uri.Host,
            uri.IsDefaultPort || uri.Port < 0 ? DefaultPort : (ushort)uri.Port,
            vhost.Length == 0 ? "/" : vhost,
            user[0].Length == 0 ? "guest" : Uri.UnescapeDataString(user[0]),
            user.Length < 2 ? "guest" : Uri.UnescapeDataString(user[1]));
    }

    public override string ToString() => $"amqp://{Username}@{Host}:{Port}/{VirtualHost}";
}
