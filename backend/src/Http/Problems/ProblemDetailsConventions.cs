using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Platform.Http.Problems;

/// <summary>Fills in the members every problem details response carries (ADR-0006).</summary>
internal static class ProblemDetailsConventions
{
    public static void Apply(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = status;

        if (!problem.Extensions.TryGetValue(ApiProblems.CodeKey, out var code) || code is null)
        {
            // Framework validation errors carry an errors map but no code.
            problem.Extensions[ApiProblems.CodeKey] = problem is HttpValidationProblemDetails
                ? ApiErrorCodes.ValidationFailed
                : ApiErrorCodes.ForStatus(status);
        }

        if (problem is HttpValidationProblemDetails validation)
        {
            CamelCaseFieldNames(validation.Errors);
        }

        problem.Extensions[ApiProblems.TraceIdKey] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        problem.Instance ??= context.HttpContext.Request.Path;
    }

    /// <summary>
    /// Field names in <c>errors</c> are the JSON names the client sent (<c>name</c>, <c>items[0].title</c>), not
    /// the C# property names the validator reports.
    /// </summary>
    private static void CamelCaseFieldNames(IDictionary<string, string[]> errors)
    {
        var renamed = errors.ToArray();
        errors.Clear();
        foreach (var (field, messages) in renamed)
        {
            var name = string.Join('.', field.Split('.').Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment)));
            errors[name] = errors.TryGetValue(name, out var existing) ? [.. existing, .. messages] : messages;
        }
    }
}
