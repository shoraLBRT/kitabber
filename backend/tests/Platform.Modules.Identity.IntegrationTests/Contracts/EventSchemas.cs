using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using MassTransit.Serialization;

namespace Platform.Modules.Identity.IntegrationTests.Contracts;

/// <summary>
/// The JSON Schema of an event as it travels on the bus (ADR-0087): the <c>message</c> of MassTransit's JSON envelope,
/// written with the bus's own serializer settings, and the rules a new version of the code must keep towards a schema
/// already published in docs/contracts.
/// </summary>
public static class EventSchemas
{
    private static readonly JsonSerializerOptions _options = new(SystemTextJsonMessageSerializer.Options)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private static readonly JsonSchemaExporterOptions _exporter = new() { TreatNullObliviousAsNonNullable = true };

    /// <summary>The schema of <paramref name="type"/>, published as <paramref name="name"/>.</summary>
    public static JsonObject Generate(Type type, string name, string description)
    {
        var body = _options.GetJsonSchemaAsNode(type, _exporter).AsObject();
        var schema = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = $"urn:message:{name}",
            ["title"] = name,
            ["description"] = description,
        };
        foreach (var (key, value) in body.ToList())
        {
            body.Remove(key);
            schema[key] = value;
        }

        return schema;
    }

    /// <summary>The message as the bus writes it.</summary>
    public static JsonObject Serialize(object message) =>
        JsonSerializer.SerializeToNode(message, message.GetType(), SystemTextJsonMessageSerializer.Options)!.AsObject();

    /// <summary>
    /// What would break a reader of <paramref name="published"/> if <paramref name="current"/> took its place: a
    /// property is gone, its type changed or widened (to <c>null</c>, say), or a required one became optional. A new
    /// property, required or not, breaks no one: readers ignore what they do not know. Empty when compatible.
    /// </summary>
    public static IReadOnlyList<string> Breaks(JsonObject published, JsonObject current)
    {
        var problems = new List<string>();
        var currentProperties = current["properties"]?.AsObject() ?? [];
        foreach (var (name, property) in published["properties"]?.AsObject() ?? [])
        {
            if (currentProperties[name] is not { } now)
            {
                problems.Add($"property '{name}' was removed");
                continue;
            }

            var was = Types(property);
            var extra = Types(now).Except(was).ToList();
            if (extra.Count > 0)
            {
                problems.Add($"property '{name}' may now be {string.Join(" or ", extra)}");
            }

            if (!string.Equals(property?["format"]?.GetValue<string>(), now["format"]?.GetValue<string>(), StringComparison.Ordinal))
            {
                problems.Add($"property '{name}' changed its format");
            }

            if (property?["items"] is { } items && now["items"] is { } nowItems && Types(nowItems).Except(Types(items)).Any())
            {
                problems.Add($"items of '{name}' changed their type");
            }
        }

        var required = Required(current);
        problems.AddRange(Required(published).Where(name => !required.Contains(name))
            .Select(name => $"property '{name}' is no longer required"));
        return problems;
    }

    /// <summary>The names of every property, nested ones included.</summary>
    public static IEnumerable<string> PropertyNames(JsonNode? schema)
    {
        foreach (var (name, property) in schema?["properties"]?.AsObject() ?? [])
        {
            yield return name;
            foreach (var nested in PropertyNames(property).Concat(PropertyNames(property?["items"])))
            {
                yield return nested;
            }
        }
    }

    private static HashSet<string> Types(JsonNode? property) => property?["type"] switch
    {
        JsonArray many => [.. many.Select(t => t!.GetValue<string>())],
        JsonValue one => [one.GetValue<string>()],
        _ => ["any"],
    };

    private static HashSet<string> Required(JsonObject schema) =>
        [.. (schema["required"]?.AsArray() ?? []).Select(n => n!.GetValue<string>())];
}
