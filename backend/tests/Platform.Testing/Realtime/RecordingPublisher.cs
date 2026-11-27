using System.Collections.Concurrent;
using Platform.Realtime;

namespace Platform.Testing.Realtime;

/// <summary>
/// Keeps every event a module publishes to the realtime channel instead of sending it: a test host puts it in place of
/// <see cref="IRealtimePublisher"/> to see what readers would receive, without a hub or a client.
/// </summary>
public sealed class RecordingPublisher : IRealtimePublisher
{
    private readonly ConcurrentQueue<(RealtimeTopic Topic, string Type, object? Data)> _events = new();

    public IReadOnlyList<(RealtimeTopic Topic, string Type, object? Data)> Published => [.. _events];

    public void Clear() => _events.Clear();

    public Task PublishAsync(RealtimeTopic topic, string type, object? data, CancellationToken cancellationToken = default)
    {
        _events.Enqueue((topic, type, data));
        return Task.CompletedTask;
    }
}
