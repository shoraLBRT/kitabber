namespace Platform.Caching;

/// <summary>
/// Atomic counters that expire (ADR-0016): the store behind rate limits. Redis keeps them for every instance of a
/// host; memory is the fake for tests and is not shared between processes. Registered by
/// <see cref="CacheServiceCollectionExtensions.AddPlatformCache"/> with the same provider as the cache.
/// </summary>
public interface IRateCounter
{
    /// <summary>
    /// Adds one to the counter <paramref name="key"/> and returns its new value. A counter that did not exist starts
    /// at one and is removed after <paramref name="lifetime"/>; later increments do not extend it.
    /// </summary>
    Task<long> IncrementAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default);
}
