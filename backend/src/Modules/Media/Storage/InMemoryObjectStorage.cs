using System.Collections.Concurrent;

namespace Platform.Modules.Media.Storage;

/// <summary>
/// The fake object storage of tests (<c>Media:Storage:Provider=InMemory</c>): objects live in the process and are lost
/// with it. Not shared between instances, so never on a stand.
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (string ContentType, byte[] Bytes)> _objects = new(StringComparer.Ordinal);

    /// <summary>The keys of the stored objects, for assertions.</summary>
    public IReadOnlyCollection<string> Keys => [.. _objects.Keys];

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(content);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _objects[key] = (contentType, buffer.ToArray());
    }

    public Task<StoredObject?> FindAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_objects.TryGetValue(key, out var o) ? new StoredObject(key, o.ContentType, o.Bytes.Length) : null);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(_objects.TryGetValue(key, out var o) ? new MemoryStream(o.Bytes, writable: false) : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>Removes every object: a test's clean start.</summary>
    public void Clear() => _objects.Clear();
}
