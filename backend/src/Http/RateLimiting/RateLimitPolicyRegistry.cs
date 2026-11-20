using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Platform.Http.RateLimiting;

/// <summary>A policy a library or module declares in code, with the values used when configuration gives none.</summary>
public sealed record RateLimitPolicyDefinition(string Name, RateLimitPolicy Defaults);

/// <summary>
/// The rate limit policies of the host: those declared in code, each overridden by
/// <c>RateLimiting:Policies:{name}:PermitLimit</c>, <c>:WindowSeconds</c> and <c>:Partition</c>. A wrong value or a
/// policy in configuration that no code declares stops the host at start.
/// </summary>
public sealed class RateLimitPolicyRegistry
{
    public const string Section = "RateLimiting";
    public const string EnabledKey = Section + ":Enabled";
    public const string PoliciesSection = Section + ":Policies";

    private readonly Dictionary<string, RateLimitPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    public RateLimitPolicyRegistry(IEnumerable<RateLimitPolicyDefinition> definitions, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(configuration);

        var enabled = configuration[EnabledKey];
        Enabled = string.IsNullOrWhiteSpace(enabled)
            || (bool.TryParse(enabled, out var value)
                ? value
                : throw new InvalidOperationException($"{EnabledKey} = '{enabled}': expected true or false."));

        foreach (var definition in definitions)
        {
            _policies[definition.Name] = definition.Defaults;
        }

        foreach (var section in configuration.GetSection(PoliciesSection).GetChildren())
        {
            if (!_policies.TryGetValue(section.Key, out var policy))
            {
                throw new InvalidOperationException(
                    $"{PoliciesSection}:{section.Key}: no such rate limit policy; known: {string.Join(", ", _policies.Keys)}.");
            }

            _policies[section.Key] = policy with
            {
                PermitLimit = Positive(section, "PermitLimit") ?? policy.PermitLimit,
                Window = Positive(section, "WindowSeconds") is { } seconds ? TimeSpan.FromSeconds(seconds) : policy.Window,
                Partition = Partition(section) ?? policy.Partition,
            };
        }
    }

    /// <summary><c>RateLimiting:Enabled</c>; true unless configuration says <c>false</c>.</summary>
    public bool Enabled { get; }

    public IReadOnlyCollection<string> Names => _policies.Keys;

    /// <summary>The policy <paramref name="name"/>; an endpoint that names an undeclared policy is a programming error.</summary>
    public RateLimitPolicy Get(string name) =>
        _policies.TryGetValue(name, out var policy)
            ? policy
            : throw new InvalidOperationException(
                $"Rate limit policy '{name}' is not declared: register it with AddRateLimitPolicy.");

    private static int? Positive(IConfigurationSection section, string key)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : throw new InvalidOperationException($"{section.Path}:{key} = '{value}': expected a positive whole number.");
    }

    private static RateLimitPartition? Partition(IConfigurationSection section)
    {
        var value = section["Partition"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<RateLimitPartition>(value, ignoreCase: true, out var partition) && Enum.IsDefined(partition)
            ? partition
            : throw new InvalidOperationException(
                $"{section.Path}:Partition = '{value}': expected one of {string.Join(", ", Enum.GetNames<RateLimitPartition>())}.");
    }
}
