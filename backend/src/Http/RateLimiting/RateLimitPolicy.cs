namespace Platform.Http.RateLimiting;

/// <summary>Whose requests one counter of a policy counts (ADR-0016).</summary>
public enum RateLimitPartition
{
    /// <summary>The signed-in account; an anonymous request is counted by its IP address.</summary>
    Account,

    /// <summary>The client IP address (an IPv6 address by its /64 network), signed in or not.</summary>
    Ip,
}

/// <summary>
/// A rate limit: at most <see cref="PermitLimit"/> requests per <see cref="Window"/> for each partition. Windows are
/// fixed and aligned to the clock, so every instance of the API counts into the same window.
/// </summary>
public sealed record RateLimitPolicy(int PermitLimit, TimeSpan Window, RateLimitPartition Partition = RateLimitPartition.Account);

/// <summary>The policies the platform defines itself. A module adds its own with a module prefix, as <c>posts.create</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>Every request under <c>/api</c>.</summary>
    public const string Default = "Default";

    /// <summary>Every request under <c>/api</c> that changes data: <c>POST</c>, <c>PUT</c>, <c>PATCH</c>, <c>DELETE</c>.</summary>
    public const string Writes = "Writes";

    public static RateLimitPolicy DefaultPolicy { get; } = new(300, TimeSpan.FromMinutes(1));

    public static RateLimitPolicy WritesPolicy { get; } = new(60, TimeSpan.FromMinutes(1));
}
