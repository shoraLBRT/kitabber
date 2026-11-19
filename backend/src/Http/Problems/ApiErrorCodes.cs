using Microsoft.AspNetCore.Http;

namespace Platform.Http.Problems;

/// <summary>
/// Machine-readable error codes, sent as the <c>code</c> member of every problem details response (ADR-0006).
/// Clients branch on the code, never on <c>title</c> or <c>detail</c>. Codes are snake_case and stable:
/// a published code is never renamed. A module adds its own codes with a module prefix, for example
/// <c>posts.draft_locked</c>.
/// </summary>
public static class ApiErrorCodes
{
    public const string BadRequest = "bad_request";
    public const string ValidationFailed = "validation_failed";
    public const string InvalidCursor = "invalid_cursor";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string Conflict = "conflict";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string TooManyRequests = "too_many_requests";
    public const string InternalError = "internal_error";
    public const string Unknown = "error";

    /// <summary>The generic code for a status, used when the code that produced the problem set none.</summary>
    public static string ForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => BadRequest,
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        StatusCodes.Status422UnprocessableEntity => ValidationFailed,
        StatusCodes.Status429TooManyRequests => TooManyRequests,
        >= 500 => InternalError,
        _ => Unknown,
    };
}
