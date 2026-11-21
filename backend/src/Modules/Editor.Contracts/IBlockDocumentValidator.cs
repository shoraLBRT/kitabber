using System.Text.Json;
using System.Text.Json.Nodes;

namespace Platform.Modules.Editor.Contracts;

/// <summary>
/// Checks a post document — <c>{"version": n, "blocks": [{"id", "type", "data"}]}</c> — against the block schemas of
/// its version and cleans every inline HTML field by the allow list (FR-EDITOR, ADR-0023). Other modules store only
/// the <see cref="BlockDocumentResult.Document"/> it returns, never the document they received.
/// </summary>
public interface IBlockDocumentValidator
{
    /// <summary>The schema version new documents are written in.</summary>
    int CurrentVersion { get; }

    /// <summary>The schema versions a document may declare: the current one and every earlier one still stored.</summary>
    IReadOnlyList<int> SupportedVersions { get; }

    /// <summary>Validates <paramref name="document"/>; never throws for bad input, every problem is an error.</summary>
    BlockDocumentResult Validate(JsonElement document);
}

/// <summary>A problem in a document.</summary>
/// <param name="Path">Where it is, for example <c>blocks[2].data.level</c>; the document itself is the empty path.</param>
/// <param name="Message">What is wrong, in English, for a developer.</param>
public sealed record BlockDocumentError(string Path, string Message);

/// <summary>The outcome of <see cref="IBlockDocumentValidator.Validate"/>.</summary>
public sealed class BlockDocumentResult
{
    private BlockDocumentResult(JsonObject? document, IReadOnlyList<BlockDocumentError> errors)
    {
        Document = document;
        Errors = errors;
    }

    /// <summary>The cleaned document in canonical form; <c>null</c> when there are errors.</summary>
    public JsonObject? Document { get; }

    /// <summary>Every problem found; empty for a valid document.</summary>
    public IReadOnlyList<BlockDocumentError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static BlockDocumentResult Valid(JsonObject document) =>
        new(document ?? throw new ArgumentNullException(nameof(document)), []);

    public static BlockDocumentResult Invalid(IReadOnlyList<BlockDocumentError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("An invalid result needs at least one error.", nameof(errors));
        }

        return new(null, errors);
    }
}
