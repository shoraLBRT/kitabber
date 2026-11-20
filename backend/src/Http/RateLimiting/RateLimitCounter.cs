using System.Globalization;
using Microsoft.Extensions.Logging;
using Platform.Caching;

namespace Platform.Http.RateLimiting;

/// <summary>
/// Counts a request into a policy under a partition the caller names (ADR-0016), for limits the middleware cannot
/// partition by itself: an anonymous sign-in is counted by the address it signs in to, not only by the network.
/// The counters are the middleware's (<c>platform:rate:{policy}:{partition}:{window}</c>), with the same fixed windows.
/// </summary>
public sealed partial class RateLimitCounter(
    RateLimitPolicyRegistry policies,
    IRateCounter counter,
    TimeProvider time,
    ILogger<RateLimitCounter> logger)
{
    /// <summary>
    /// Counts one request of <paramref name="partition"/> into <paramref name="policy"/>; the time to the next window
    /// when the partition is over the limit, otherwise <c>null</c>. Rate limiting switched off, or a failing counter
    /// store, let the request through, as in the middleware.
    /// </summary>
    public async Task<TimeSpan?> CountAsync(string policy, string partition, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        if (!policies.Enabled)
        {
            return null;
        }

        try
        {
            return await CountAsync(counter, time, policy, policies.Get(policy), partition, cancellationToken);
        }
#pragma warning disable CA1031 // Any failure of the counter store lets the request through.
        catch (Exception exception) when (exception is not InvalidOperationException
                                          && !cancellationToken.IsCancellationRequested)
        {
            LogCounterFailed(logger, exception);
            return null;
        }
#pragma warning restore CA1031
    }

    // Fixed windows aligned to the Unix epoch: window n covers [n * length, (n + 1) * length). Returns the time to
    // the next window when the request is over the limit.
    internal static async Task<TimeSpan?> CountAsync(
        IRateCounter counter, TimeProvider time, string name, RateLimitPolicy policy, string partition,
        CancellationToken cancellationToken)
    {
        var length = (long)policy.Window.TotalMilliseconds;
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        var window = now / length;
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{RateLimitingMiddleware.KeyPrefix}{name.ToLowerInvariant()}:{partition}:{window}");
        var count = await counter.IncrementAsync(key, policy.Window, cancellationToken);
        return count > policy.PermitLimit ? TimeSpan.FromMilliseconds(((window + 1) * length) - now) : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit counters are unavailable; the request is let through")]
    private static partial void LogCounterFailed(ILogger logger, Exception exception);
}
