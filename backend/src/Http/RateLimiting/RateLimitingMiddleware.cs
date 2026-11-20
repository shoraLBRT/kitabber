using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Caching;
using Platform.Http.Problems;

namespace Platform.Http.RateLimiting;

/// <summary>Endpoint metadata: the endpoint also counts into these policies (<see cref="RateLimitingEndpointExtensions"/>).</summary>
public sealed record RateLimitMetadata(string Policy);

/// <summary>Endpoint metadata: no rate limit applies to the endpoint.</summary>
public sealed class NoRateLimitMetadata
{
    public static NoRateLimitMetadata Instance { get; } = new();
}

/// <summary>
/// Counts every request under <c>/api</c> into its policies (ADR-0016) and answers 429 with <c>Retry-After</c> and
/// problem details <c>too_many_requests</c> once one is over its limit. Runs after authentication, so a policy can
/// count by account, and after routing, so it sees the endpoint's policies. When the counter store fails the request
/// passes: a Redis outage must not take the whole API down, and readiness already reports it.
/// </summary>
internal sealed partial class RateLimitingMiddleware(
    RequestDelegate next,
    RateLimitPolicyRegistry policies,
    IRateCounter counter,
    TimeProvider time,
    ILogger<RateLimitingMiddleware> logger)
{
    /// <summary>The prefix of every counter key: <c>platform:rate:{policy}:{partition}:{window}</c> in Redis.</summary>
    public const string KeyPrefix = "rate:";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!policies.Enabled
            || !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || context.GetEndpoint()?.Metadata.GetMetadata<NoRateLimitMetadata>() is not null)
        {
            await next(context);
            return;
        }

        TimeSpan? retryAfter = null;
        try
        {
            foreach (var name in PolicyNames(context))
            {
                var policy = policies.Get(name);
                var wait = await CountAsync(context, name, policy);
                if (wait is { } value && (retryAfter is null || value > retryAfter))
                {
                    retryAfter = value;
                }
            }
        }
#pragma warning disable CA1031 // Any failure of the counter store lets the request through.
        catch (Exception exception) when (exception is not InvalidOperationException
                                          && !context.RequestAborted.IsCancellationRequested)
        {
            LogCounterFailed(logger, exception);
            retryAfter = null;
        }
#pragma warning restore CA1031

        if (retryAfter is not { } delay)
        {
            await next(context);
            return;
        }

        var seconds = Math.Max(1, (long)Math.Ceiling(delay.TotalSeconds));
        LogRejected(logger, context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        await ApiProblems.Problem(
                StatusCodes.Status429TooManyRequests,
                ApiErrorCodes.TooManyRequests,
                "Too many requests",
                $"Rate limit exceeded; retry in {seconds} s.")
            .ExecuteAsync(context);
    }

    /// <summary>The partition of a request, as the counter key part: <c>a:{account}</c> or <c>ip:{address}</c>.</summary>
    internal static string PartitionKey(HttpContext context, RateLimitPartition partition)
    {
        if (partition == RateLimitPartition.Account && context.User.Identity?.IsAuthenticated == true
            && (context.User.FindFirst(ClaimTypes.NameIdentifier) ?? context.User.FindFirst("sub")) is { } account)
        {
            return "a:" + account.Value;
        }

        return "ip:" + ClientAddress.Network(context.Connection.RemoteIpAddress);
    }

    private static IEnumerable<string> PolicyNames(HttpContext context)
    {
        yield return RateLimitPolicies.Default;
        if (!(HttpMethods.IsGet(context.Request.Method)
              || HttpMethods.IsHead(context.Request.Method)
              || HttpMethods.IsOptions(context.Request.Method)))
        {
            yield return RateLimitPolicies.Writes;
        }

        if (context.GetEndpoint() is { } endpoint)
        {
            foreach (var metadata in endpoint.Metadata.GetOrderedMetadata<RateLimitMetadata>().DistinctBy(m => m.Policy))
            {
                yield return metadata.Policy;
            }
        }
    }

    // Fixed windows aligned to the Unix epoch: window n covers [n * length, (n + 1) * length). Returns the time to
    // the next window when the request is over the limit.
    private async Task<TimeSpan?> CountAsync(HttpContext context, string name, RateLimitPolicy policy)
    {
        var length = (long)policy.Window.TotalMilliseconds;
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        var window = now / length;
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{KeyPrefix}{name.ToLowerInvariant()}:{PartitionKey(context, policy.Partition)}:{window}");
        var count = await counter.IncrementAsync(key, policy.Window, context.RequestAborted);
        return count > policy.PermitLimit ? TimeSpan.FromMilliseconds(((window + 1) * length) - now) : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit counters are unavailable; the request is let through")]
    private static partial void LogCounterFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rate limit exceeded: {Method} {Path}")]
    private static partial void LogRejected(ILogger logger, string method, PathString path);
}

/// <summary>How rate limits name a client address (ADR-0016).</summary>
public static class ClientAddress
{
    /// <summary>
    /// The client address as counted: an IPv4 address as is, an IPv6 address by its /64 network, since one
    /// subscriber usually gets a whole /64.
    /// </summary>
    public static string Network(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
