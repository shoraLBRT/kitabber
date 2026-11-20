using Microsoft.AspNetCore.SignalR;

namespace Platform.Realtime;

/// <summary>An event as the client receives it: the topic it came through, its type and its data.</summary>
public sealed record RealtimeEvent(string Topic, string Type, object? Data);

/// <summary>The methods the server calls on a client. The client handles <c>receive</c> only.</summary>
public interface IRealtimeClient
{
    Task Receive(RealtimeEvent realtimeEvent);
}

/// <summary>
/// The one hub of the platform (<see cref="RealtimeServiceCollectionExtensions.HubPath"/>), signed-in clients only.
/// A connection joins its own account's topic on connect; any other topic it asks for with <see cref="Subscribe"/>,
/// which the topic kind's <see cref="IRealtimeTopicPolicy"/> allows or refuses. Clients only listen: everything
/// they do goes through the REST API.
/// </summary>
public sealed class RealtimeHub(IEnumerable<IRealtimeTopicPolicy> policies) : Hub<IRealtimeClient>
{
    /// <summary>The error a refused or unknown topic gives, as the <see cref="HubException"/> message.</summary>
    public const string TopicRefused = "topic_refused";

    public override async Task OnConnectedAsync()
    {
        // The hub requires an authenticated account (RealtimeServiceCollectionExtensions.MapRealtime).
        var account = RealtimeAccounts.Of(Context.User) ?? throw new HubException(TopicRefused);
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeTopic.Account(account).Name);
        await base.OnConnectedAsync();
    }

    /// <summary>Starts receiving the events of <paramref name="topic"/> (<c>kind:uuid</c>).</summary>
    public async Task Subscribe(string topic)
    {
        var parsed = await AllowedAsync(topic);
        await Groups.AddToGroupAsync(Context.ConnectionId, parsed.Name, Context.ConnectionAborted);
    }

    /// <summary>Stops receiving the events of <paramref name="topic"/>.</summary>
    public async Task Unsubscribe(string topic)
    {
        if (!RealtimeTopic.TryParse(topic, out var parsed))
        {
            throw new HubException(TopicRefused);
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, parsed.Name, Context.ConnectionAborted);
    }

    private async Task<RealtimeTopic> AllowedAsync(string topic)
    {
        if (RealtimeTopic.TryParse(topic, out var parsed)
            && policies.LastOrDefault(p => p.Kind == parsed.Kind) is { } policy
            && Context.User is { } user
            && await policy.CanSubscribeAsync(user, parsed, Context.ConnectionAborted))
        {
            return parsed;
        }

        throw new HubException(TopicRefused);
    }
}

/// <summary>Sends events to the clients of a topic, on every API instance (the Redis backplane, ADR-0019).</summary>
public interface IRealtimePublisher
{
    Task PublishAsync(RealtimeTopic topic, string type, object? data, CancellationToken cancellationToken = default);
}

internal sealed class HubRealtimePublisher(IHubContext<RealtimeHub, IRealtimeClient> hub) : IRealtimePublisher
{
    public Task PublishAsync(RealtimeTopic topic, string type, object? data, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return hub.Clients.Group(topic.Name).Receive(new RealtimeEvent(topic.Name, type, data));
    }
}
