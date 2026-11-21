using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Platform.Modules.Editor.Contracts;

namespace Platform.Modules.Editor.Blocks;

/// <summary>
/// Validates a post document against the schemas of the version it declares (ADR-0023). The document is
/// <c>{"version", "blocks"}</c>; a block is <c>{"id", "type", "data"}</c>. Nothing else is accepted at any level, and
/// every error names its place. The result is rebuilt from the schema, so it holds only known fields, in a fixed
/// order, with inline HTML cleaned.
/// </summary>
public sealed partial class BlockDocumentValidator : IBlockDocumentValidator
{
    /// <summary>At most this many errors are reported; a document past that is broken as a whole.</summary>
    public const int MaxErrors = 50;

    private static readonly string[] _documentFields = ["version", "blocks"];
    private static readonly string[] _blockFields = ["id", "type", "data"];

    private readonly Dictionary<int, BlockSchemaSet> _versions;

    public BlockDocumentValidator(IEnumerable<BlockSchemaSet> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        _versions = versions.ToDictionary(v => v.Version);
        if (_versions.Count == 0)
        {
            throw new ArgumentException("At least one schema version is needed.", nameof(versions));
        }

        SupportedVersions = [.. _versions.Keys.Order()];
        CurrentVersion = SupportedVersions[^1];
    }

    public int CurrentVersion { get; }

    public IReadOnlyList<int> SupportedVersions { get; }

    public BlockDocumentResult Validate(JsonElement document)
    {
        var errors = new List<BlockDocumentError>();
        var result = Read(document, errors);
        return errors.Count == 0 && result is not null
            ? BlockDocumentResult.Valid(result)
            : BlockDocumentResult.Invalid(errors.Count == 0 ? [new(string.Empty, "Invalid document.")] : [.. errors.Take(MaxErrors)]);
    }

    private JsonObject? Read(JsonElement document, List<BlockDocumentError> errors)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new(string.Empty, "Must be an object."));
            return null;
        }

        var given = JsonObjects.Properties(document, string.Empty, errors);
        Unknown(given, _documentFields, string.Empty, "document", errors);

        if (!given.TryGetValue("version", out var versionValue))
        {
            errors.Add(new("version", "Required."));
            return null;
        }

        if (versionValue.ValueKind != JsonValueKind.Number || !versionValue.TryGetInt32(out var version)
            || !_versions.TryGetValue(version, out var schemas))
        {
            errors.Add(new("version", $"Unsupported schema version; supported: {string.Join(", ", SupportedVersions)}."));
            return null;
        }

        if (!given.TryGetValue("blocks", out var blocksValue) || blocksValue.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new("blocks", "Must be an array of blocks."));
            return null;
        }

        var blocks = new JsonArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var block in blocksValue.EnumerateArray())
        {
            if (ReadBlock(block, $"blocks[{index}]", schemas, ids, errors) is { } read)
            {
                blocks.Add(read);
            }

            index++;
            if (errors.Count >= MaxErrors)
            {
                break;
            }
        }

        return new JsonObject { ["version"] = version, ["blocks"] = blocks };
    }

    private static JsonObject? ReadBlock(
        JsonElement block, string path, BlockSchemaSet schemas, HashSet<string> ids, List<BlockDocumentError> errors)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new(path, "Must be an object."));
            return null;
        }

        var before = errors.Count;
        var given = JsonObjects.Properties(block, path, errors);
        Unknown(given, _blockFields, path, "block", errors);

        string? id = null;
        if (!given.TryGetValue("id", out var idValue) || idValue.ValueKind != JsonValueKind.String
            || !BlockId().IsMatch(id = idValue.GetString()!))
        {
            errors.Add(new($"{path}.id", "Must be 1 to 64 characters: Latin letters, digits, '-' or '_'."));
        }
        else if (!ids.Add(id))
        {
            errors.Add(new($"{path}.id", "Another block has this id."));
        }

        BlockSchema? schema = null;
        if (!given.TryGetValue("type", out var typeValue) || typeValue.ValueKind != JsonValueKind.String)
        {
            errors.Add(new($"{path}.type", "Required string."));
        }
        else if (!schemas.Blocks.TryGetValue(typeValue.GetString()!, out schema))
        {
            errors.Add(new($"{path}.type", $"Unknown block type in schema version {schemas.Version}."));
        }

        JsonObject? data = null;
        if (schema is not null)
        {
            if (given.TryGetValue("data", out var dataValue))
            {
                data = schema.Read(dataValue, $"{path}.data", errors);
            }
            else
            {
                errors.Add(new($"{path}.data", "Required."));
            }
        }

        return errors.Count == before && schema is not null && data is not null
            ? new JsonObject { ["id"] = id, ["type"] = schema.Type, ["data"] = data }
            : null;
    }

    private static void Unknown(
        Dictionary<string, JsonElement> given, string[] known, string path, string what, List<BlockDocumentError> errors)
    {
        foreach (var name in given.Keys.Where(name => !known.Contains(name, StringComparer.Ordinal)))
        {
            errors.Add(new(JsonObjects.Child(path, name), $"Unknown field of the {what}."));
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex BlockId();
}
