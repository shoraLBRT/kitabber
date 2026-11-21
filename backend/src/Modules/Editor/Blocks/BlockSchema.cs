using System.Text.Json;
using System.Text.Json.Nodes;
using Platform.Modules.Editor.Contracts;

namespace Platform.Modules.Editor.Blocks;

/// <summary>
/// The schema of one block type: the fields its <c>data</c> object has. A field outside the list is an error, not
/// something to drop silently: a client that sends it has a different idea of the block.
/// </summary>
public sealed class BlockSchema
{
    public BlockSchema(string type, params IReadOnlyList<BlockField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(fields);
        Type = type;
        Fields = fields;
    }

    /// <summary>The value of the block's <c>type</c>, for example <c>paragraph</c>.</summary>
    public string Type { get; }

    public IReadOnlyList<BlockField> Fields { get; }

    /// <summary>
    /// A check across fields, run on the canonical <c>data</c> once every field has been read without errors; it adds
    /// its errors under the path of <c>data</c>.
    /// </summary>
    public Action<JsonObject, string, ICollection<BlockDocumentError>>? Check { get; init; }

    /// <summary>Reads <paramref name="data"/> into canonical form (fields in schema order, defaults filled in).</summary>
    public JsonObject? Read(JsonElement data, string path, ICollection<BlockDocumentError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (data.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new(path, "Must be an object."));
            return null;
        }

        var before = errors.Count;
        var given = JsonObjects.Properties(data, path, errors);
        foreach (var name in given.Keys.Where(name => !Fields.Any(f => f.Name == name)))
        {
            errors.Add(new(JsonObjects.Child(path, name), $"Unknown field of block '{Type}'."));
        }

        var result = new JsonObject();
        foreach (var field in Fields)
        {
            if (given.TryGetValue(field.Name, out var value))
            {
                result[field.Name] = field.Read(value, JsonObjects.Child(path, field.Name), errors);
            }
            else if (field.IsRequired)
            {
                errors.Add(new(JsonObjects.Child(path, field.Name), "Required."));
            }
            else
            {
                result[field.Name] = field.DefaultValue!.DeepClone();
            }
        }

        if (errors.Count == before)
        {
            Check?.Invoke(result, path, errors);
        }

        return errors.Count == before ? result : null;
    }
}

/// <summary>
/// The block schemas of one document version. A version is closed once released: a change that would reject or
/// reinterpret a stored document makes a new version, and the old one stays to read what is stored (ADR-0023).
/// </summary>
public sealed class BlockSchemaSet
{
    public BlockSchemaSet(int version, IEnumerable<BlockSchema> blocks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentNullException.ThrowIfNull(blocks);
        Version = version;
        Blocks = blocks.ToDictionary(b => b.Type, StringComparer.Ordinal);
    }

    public int Version { get; }

    public IReadOnlyDictionary<string, BlockSchema> Blocks { get; }
}

internal static class JsonObjects
{
    public static string Child(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    /// <summary>The properties of an object; a name given twice is an error and only its first value counts.</summary>
    public static Dictionary<string, JsonElement> Properties(
        JsonElement value, string path, ICollection<BlockDocumentError> errors)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                errors.Add(new(Child(path, property.Name), "Given more than once."));
            }
        }

        return properties;
    }
}
