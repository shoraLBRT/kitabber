using System.Text.Json;
using System.Text.Json.Nodes;
using Platform.Modules.Editor.Contracts;

namespace Platform.Modules.Editor.Blocks;

/// <summary>One field of a block's <c>data</c>: its name, whether it must be present, and how its value is read.</summary>
public abstract class BlockField
{
    protected BlockField(string name, JsonNode? defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }

    /// <summary>The value written when the field is absent; a field without one is required.</summary>
    public JsonNode? DefaultValue { get; }

    public bool IsRequired => DefaultValue is null;

    /// <summary>The value in canonical form, or <c>null</c> after adding an error at <paramref name="path"/>.</summary>
    public abstract JsonNode? Read(JsonElement value, string path, ICollection<BlockDocumentError> errors);
}

/// <summary>A string of inline HTML, cleaned by <see cref="InlineHtml"/>.</summary>
public sealed class InlineHtmlField(string name, InlineHtml html, string? defaultValue = null)
    : BlockField(name, defaultValue is null ? null : JsonValue.Create(defaultValue))
{
    public override JsonNode? Read(JsonElement value, string path, ICollection<BlockDocumentError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(new(path, "Must be a string."));
            return null;
        }

        return JsonValue.Create(html.Clean(value.GetString()!));
    }
}

/// <summary>A string from a fixed set.</summary>
public sealed class ChoiceField(string name, IReadOnlyList<string> choices, string? defaultValue = null)
    : BlockField(name, defaultValue is null ? null : JsonValue.Create(defaultValue))
{
    public IReadOnlyList<string> Choices { get; } = choices;

    public override JsonNode? Read(JsonElement value, string path, ICollection<BlockDocumentError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value.ValueKind == JsonValueKind.String && Choices.Contains(value.GetString(), StringComparer.Ordinal))
        {
            return JsonValue.Create(value.GetString());
        }

        errors.Add(new(path, $"Must be one of: {string.Join(", ", Choices)}."));
        return null;
    }
}

/// <summary>An integer from a fixed set.</summary>
public sealed class IntegerChoiceField(string name, IReadOnlyList<int> choices)
    : BlockField(name, null)
{
    public IReadOnlyList<int> Choices { get; } = choices;

    public override JsonNode? Read(JsonElement value, string path, ICollection<BlockDocumentError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && Choices.Contains(number))
        {
            return JsonValue.Create(number);
        }

        errors.Add(new(path, $"Must be one of: {string.Join(", ", Choices)}."));
        return null;
    }
}

/// <summary>A non-empty array of inline HTML strings, each cleaned by <see cref="InlineHtml"/>.</summary>
public sealed class InlineHtmlListField(string name, InlineHtml html) : BlockField(name, null)
{
    public override JsonNode? Read(JsonElement value, string path, ICollection<BlockDocumentError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            errors.Add(new(path, "Must be a non-empty array of strings."));
            return null;
        }

        var items = new JsonArray();
        var index = 0;
        var valid = true;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                items.Add(JsonValue.Create(html.Clean(item.GetString()!)));
            }
            else
            {
                errors.Add(new($"{path}[{index}]", "Must be a string."));
                valid = false;
            }

            index++;
        }

        return valid ? items : null;
    }
}
