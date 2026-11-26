using System.Globalization;
using StackExchange.Redis;

namespace Platform.Caching;

/// <summary>
/// The write buffer in Redis (ADR-0083). A buffer <c>name</c> is a list of pending entries
/// <c>platform:buffer:{name}:pending</c>; a claim renames it to a batch key and records the batch with its lease in
/// the sorted set <c>platform:buffer:{name}:batches</c>. The braces keep every key of a buffer in one cluster slot,
/// so each step is one atomic script.
/// </summary>
internal sealed class RedisWriteBuffer(IConnectionMultiplexer redis, TimeProvider time) : IWriteBuffer
{
    private const string _appendOnceScript = """
        if redis.call('SET', KEYS[1], '1', 'NX', 'PX', ARGV[1]) then
          redis.call('RPUSH', KEYS[2], ARGV[2])
          return 1
        end
        return 0
        """;

    // A batch whose lease ran out goes to the next claimer first; otherwise the pending list becomes a new batch.
    private const string _claimScript = """
        local now = tonumber(ARGV[1])
        local until_ = now + tonumber(ARGV[2])
        local expired = redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', now, 'LIMIT', 0, 1)
        if #expired > 0 then
          redis.call('ZADD', KEYS[2], until_, expired[1])
          return expired[1]
        end
        if redis.call('EXISTS', KEYS[1]) == 1 then
          redis.call('RENAME', KEYS[1], KEYS[3])
          redis.call('ZADD', KEYS[2], until_, KEYS[3])
          return KEYS[3]
        end
        return false
        """;

    private const string _completeScript = """
        redis.call('DEL', KEYS[2])
        redis.call('ZREM', KEYS[1], KEYS[2])
        return 1
        """;

    public async Task<bool> AppendOnceAsync(
        string buffer, string once, TimeSpan lifetime, string entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(once);
        ArgumentNullException.ThrowIfNull(entry);
        var milliseconds = Math.Max(1, (long)Math.Ceiling(lifetime.TotalMilliseconds));
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            _appendOnceScript,
            [new RedisKey(Prefix(buffer) + "once:" + once), new RedisKey(Pending(buffer))],
            [milliseconds.ToString(CultureInfo.InvariantCulture), entry]);
        return (long)result == 1;
    }

    public async Task AppendAsync(string buffer, string entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await redis.GetDatabase().ListRightPushAsync(new RedisKey(Pending(buffer)), entry);
    }

    public async Task<WriteBatch?> ClaimAsync(
        string buffer, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var fresh = Prefix(buffer) + "batch:" + Guid.CreateVersion7(now).ToString("N");
        var database = redis.GetDatabase();
        var claimed = await database.ScriptEvaluateAsync(
            _claimScript,
            [new RedisKey(Pending(buffer)), new RedisKey(Batches(buffer)), new RedisKey(fresh)],
            [
                now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                Math.Max(1, (long)lease.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
            ]);
        if (claimed.IsNull)
        {
            return null;
        }

        var key = (string)claimed!;
        var entries = await database.ListRangeAsync(new RedisKey(key));
        return new WriteBatch(buffer, key, [.. entries.Select(e => (string)e!)]);
    }

    public async Task CompleteAsync(WriteBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await redis.GetDatabase().ScriptEvaluateAsync(
            _completeScript, [new RedisKey(Batches(batch.Buffer)), new RedisKey(batch.Id)]);
    }

    private static string Prefix(string buffer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buffer);
        return $"{CacheServiceCollectionExtensions.InstanceName}buffer:{{{buffer}}}:";
    }

    private static string Pending(string buffer) => Prefix(buffer) + "pending";

    private static string Batches(string buffer) => Prefix(buffer) + "batches";
}

/// <summary>The write buffer in process memory, on the host's <see cref="TimeProvider"/>: the fake for tests.</summary>
internal sealed class InMemoryWriteBuffer(TimeProvider time) : IWriteBuffer
{
    private readonly Dictionary<string, Buffer> _buffers = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public Task<bool> AppendOnceAsync(
        string buffer, string once, TimeSpan lifetime, string entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(once);
        ArgumentNullException.ThrowIfNull(entry);
        var now = time.GetUtcNow();
        lock (_lock)
        {
            var state = Of(buffer);
            if (state.Marks.Count > 10_000)
            {
                foreach (var stale in state.Marks.Where(m => m.Value <= now).Select(m => m.Key).ToList())
                {
                    state.Marks.Remove(stale);
                }
            }

            if (state.Marks.TryGetValue(once, out var expires) && expires > now)
            {
                return Task.FromResult(false);
            }

            state.Marks[once] = now + lifetime;
            state.Pending.Add(entry);
            return Task.FromResult(true);
        }
    }

    public Task AppendAsync(string buffer, string entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_lock)
        {
            Of(buffer).Pending.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<WriteBatch?> ClaimAsync(string buffer, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        lock (_lock)
        {
            var state = Of(buffer);
            var expired = state.Batches.FirstOrDefault(b => b.Value.Until <= now);
            if (expired.Key is not null)
            {
                state.Batches[expired.Key] = (expired.Value.Entries, now + lease);
                return Task.FromResult<WriteBatch?>(new WriteBatch(buffer, expired.Key, [.. expired.Value.Entries]));
            }

            if (state.Pending.Count == 0)
            {
                return Task.FromResult<WriteBatch?>(null);
            }

            var id = Guid.CreateVersion7(now).ToString("N");
            var entries = state.Pending;
            state.Pending = [];
            state.Batches[id] = (entries, now + lease);
            return Task.FromResult<WriteBatch?>(new WriteBatch(buffer, id, [.. entries]));
        }
    }

    public Task CompleteAsync(WriteBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        lock (_lock)
        {
            Of(batch.Buffer).Batches.Remove(batch.Id);
        }

        return Task.CompletedTask;
    }

    private Buffer Of(string buffer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buffer);
        if (!_buffers.TryGetValue(buffer, out var state))
        {
            state = new Buffer();
            _buffers[buffer] = state;
        }

        return state;
    }

    private sealed class Buffer
    {
        public List<string> Pending { get; set; } = [];

        public Dictionary<string, DateTimeOffset> Marks { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, (List<string> Entries, DateTimeOffset Until)> Batches { get; } =
            new(StringComparer.Ordinal);
    }
}
