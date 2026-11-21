namespace Platform.Modules.Media.Storage;

/// <summary>An object as the storage keeps it: its bytes are read with <see cref="IObjectStorage.OpenReadAsync"/>.</summary>
/// <param name="Key">The object's key.</param>
/// <param name="ContentType">The MIME type the object was saved with.</param>
/// <param name="Size">The size in bytes.</param>
public sealed record StoredObject(string Key, string ContentType, long Size);

/// <summary>
/// The object storage of media (ADR-0030): an S3-compatible service on the stands, MinIO locally
/// (<see cref="S3ObjectStorage"/>), and <see cref="InMemoryObjectStorage"/> in tests. Keys are opaque strings made by
/// <see cref="StorageKeys"/>; the storage knows nothing of owners or kinds.
/// </summary>
public interface IObjectStorage
{
    /// <summary>Saves <paramref name="content"/> under <paramref name="key"/>, replacing an object with that key.</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>The object's description, or <c>null</c> when there is no object with <paramref name="key"/>.</summary>
    Task<StoredObject?> FindAsync(string key, CancellationToken cancellationToken);

    /// <summary>The object's bytes, or <c>null</c> when there is no object with <paramref name="key"/>. Dispose the stream.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes the object; removing a missing one is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
