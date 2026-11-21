using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Editor.Contracts;

namespace Platform.Modules.Editor;

/// <summary>
/// <c>/api/v1/editor</c> (FR-EDITOR, ADR-0023): the editor checks a document against the server's block schemas
/// before it saves or publishes, and gets back the cleaned document the server would store.
/// </summary>
public sealed class EditorEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        var editor = v1.MapGroup("/editor").RequireAuthorization().WithTags("Editor");
        editor.MapPost("/documents/validation", Validate)
            .Produces<JsonObject>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("ValidateEditorDocument")
            .WithSummary("Checks a post document against the block schemas and returns it cleaned");
    }

    private static IResult Validate(JsonElement document, IBlockDocumentValidator validator)
    {
        var result = validator.Validate(document);
        if (result.IsValid)
        {
            return TypedResults.Ok(result.Document);
        }

        var errors = result.Errors
            .GroupBy(e => e.Path, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal);
        return ApiProblems.Validation(errors, "The document does not match the block schemas.");
    }
}
