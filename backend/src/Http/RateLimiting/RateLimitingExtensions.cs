using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Caching;
using Platform.Configuration;

namespace Platform.Http.RateLimiting;

/// <summary>Registers the rate limits of ADR-0016 in the Api host and lets a module declare its own policies.</summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// The built-in policies (<see cref="RateLimitPolicies"/>), their counters in the platform cache (Redis, so
    /// every instance shares them) and the middleware that <see cref="ApiApplicationExtensions.UsePlatformApi"/>
    /// puts after authentication.
    /// </summary>
    public static IServiceCollection AddPlatformRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPlatformCache(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RateLimitPolicyRegistry>();
        services.AddRateLimitPolicy(RateLimitPolicies.Default, RateLimitPolicies.DefaultPolicy);
        services.AddRateLimitPolicy(RateLimitPolicies.Writes, RateLimitPolicies.WritesPolicy);
        return services;
    }

    /// <summary>
    /// Declares a policy an endpoint can name with <see cref="RateLimitingEndpointExtensions.WithRateLimit"/>.
    /// Configuration may override its values under <c>RateLimiting:Policies:{name}</c>; add those keys to the
    /// module's settings and docs/CONFIG.md.
    /// </summary>
    public static IServiceCollection AddRateLimitPolicy(this IServiceCollection services, string name, RateLimitPolicy defaults)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(defaults);
        if (defaults.PermitLimit <= 0 || defaults.Window < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentException("A policy allows at least one request per window of at least one second.", nameof(defaults));
        }

        services.AddSingleton(new RateLimitPolicyDefinition(name, defaults));
        return services;
    }
}

/// <summary>Rate limits of one endpoint or group.</summary>
public static class RateLimitingEndpointExtensions
{
    /// <summary>Counts the endpoint into <paramref name="policy"/> as well as the built-in policies.</summary>
    public static TBuilder WithRateLimit<TBuilder>(this TBuilder builder, string policy)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        return builder.WithMetadata(new RateLimitMetadata(policy));
    }

    /// <summary>No rate limit applies to the endpoint, not even the built-in ones. Needs a reason in a comment.</summary>
    public static TBuilder WithoutRateLimit<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(NoRateLimitMetadata.Instance);
}

/// <summary>The settings rate limiting and the trusted proxies read (docs/CONFIG.md).</summary>
public static class RateLimitingSettings
{
    /// <summary>The networks of the reverse proxies whose <c>X-Forwarded-For</c> gives the client address.</summary>
    public const string TrustedProxiesKey = "Http:TrustedProxies";

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(RateLimitPolicyRegistry.EnabledKey, "rate limits of the API: true, or false to switch them off")
        {
            Default = "true",
            AllowedValues = ["true", "false"],
        },
        .. Policy(RateLimitPolicies.Default, RateLimitPolicies.DefaultPolicy, "every request under /api"),
        .. Policy(RateLimitPolicies.Writes, RateLimitPolicies.WritesPolicy, "every POST, PUT, PATCH and DELETE under /api"),
        new(TrustedProxiesKey,
            "networks of the reverse proxies whose X-Forwarded-For is the client address, comma-separated CIDR: " +
            "10.0.0.0/8,172.16.0.0/12; without it only a proxy on the same machine (loopback) is trusted"),
    ];

    /// <summary>The three settings of a policy, for a module that declares one.</summary>
    public static IEnumerable<Setting> Policy(string name, RateLimitPolicy defaults, string scope)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        var prefix = $"{RateLimitPolicyRegistry.PoliciesSection}:{name}";
        yield return new($"{prefix}:PermitLimit", $"requests allowed per window, {scope}")
        {
            Default = defaults.PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        yield return new($"{prefix}:WindowSeconds", $"length of the window in seconds, {scope}")
        {
            Default = ((int)defaults.Window.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        yield return new($"{prefix}:Partition", $"whom one counter counts, {scope}: Account (IP for anonymous) or Ip")
        {
            Default = defaults.Partition.ToString(),
            AllowedValues = Enum.GetNames<RateLimitPartition>(),
        };
    }
}
