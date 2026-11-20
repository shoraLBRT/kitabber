using System.Globalization;
using StackExchange.Redis;

namespace Platform.Caching;

/// <summary>Counters in Redis: one <c>INCR</c> and, for a new key, <c>PEXPIRE</c> in a single atomic script.</summary>
internal sealed class RedisRateCounter(IConnectionMultiplexer redis) : IRateCounter
{
    // A script runs atomically, so two instances never both see "new key" or leave a key without an expiry.
    private const string _script = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
          redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return count
        """;

    public async Task<long> IncrementAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var milliseconds = Math.Max(1, (long)Math.Ceiling(lifetime.TotalMilliseconds));
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            _script,
            [new RedisKey(CacheServiceCollectionExtensions.InstanceName + key)],
            [milliseconds.ToString(CultureInfo.InvariantCulture)]);
        return (long)result;
    }
}

/// <summary>Counters in process memory, on the host's <see cref="TimeProvider"/>: the fake for tests.</summary>
internal sealed class InMemoryRateCounter(TimeProvider time) : IRateCounter
{
    private readonly Dictionary<string, (long Count, DateTimeOffset Expires)> _counters = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public Task<long> IncrementAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var now = time.GetUtcNow();
        lock (_lock)
        {
            if (_counters.Count > 10_000)
            {
                foreach (var stale in _counters.Where(pair => pair.Value.Expires <= now).Select(pair => pair.Key).ToList())
                {
                    _counters.Remove(stale);
                }
            }

            var (count, expires) = _counters.TryGetValue(key, out var current) && current.Expires > now
                ? (current.Count + 1, current.Expires)
                : (1L, now + lifetime);
            _counters[key] = (count, expires);
            return Task.FromResult(count);
        }
    }
}
