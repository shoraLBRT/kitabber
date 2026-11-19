using Microsoft.AspNetCore.Http;

namespace Platform.Http.Problems;

/// <summary>Problem details results with a code, for endpoints that return a failure instead of throwing.</summary>
public static class ApiProblems
{
    /// <summary>The name of the code member in a problem details body.</summary>
    public const string CodeKey = "code";

    /// <summary>The name of the trace id member in a problem details body.</summary>
    public const string TraceIdKey = "traceId";

    public static IResult Problem(int status, string code, string title, string? detail = null) =>
        TypedResults.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?> { [CodeKey] = code });

    public static IResult NotFound(string code = ApiErrorCodes.NotFound, string? detail = null) =>
        Problem(StatusCodes.Status404NotFound, code, "Not found", detail);

    public static IResult Conflict(string code = ApiErrorCodes.Conflict, string? detail = null) =>
        Problem(StatusCodes.Status409Conflict, code, "Conflict", detail);

    public static IResult Forbidden(string code = ApiErrorCodes.Forbidden, string? detail = null) =>
        Problem(StatusCodes.Status403Forbidden, code, "Forbidden", detail);

    /// <summary>A validation failure: <paramref name="errors"/> maps a field (camelCase) to its messages.</summary>
    public static IResult Validation(IDictionary<string, string[]> errors, string? detail = null) =>
        TypedResults.ValidationProblem(
            errors,
            detail: detail,
            title: "Validation failed",
            extensions: new Dictionary<string, object?> { [CodeKey] = ApiErrorCodes.ValidationFailed });
}
