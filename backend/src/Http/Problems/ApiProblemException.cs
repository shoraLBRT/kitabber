namespace Platform.Http.Problems;

/// <summary>
/// An expected failure that becomes a problem details response with the given status and code.
/// Throw it from anywhere below an endpoint; the host's exception handler turns it into the response.
/// Endpoints may also return <see cref="ApiProblems"/> results directly.
/// </summary>
public sealed class ApiProblemException : Exception
{
    public ApiProblemException(int status, string code, string title, string? detail = null)
        : base(detail ?? title)
    {
        Status = status;
        Code = code;
        Title = title;
        Detail = detail;
    }

    public ApiProblemException()
        : this(500, ApiErrorCodes.InternalError, "Internal error")
    {
    }

    public ApiProblemException(string message)
        : this(500, ApiErrorCodes.InternalError, "Internal error", message)
    {
    }

    public ApiProblemException(string message, Exception innerException)
        : base(message, innerException)
    {
        Status = 500;
        Code = ApiErrorCodes.InternalError;
        Title = "Internal error";
        Detail = message;
    }

    public int Status { get; }

    public string Code { get; }

    public string Title { get; }

    public string? Detail { get; }

    /// <summary>Extra members added to the problem details, for example the offending field.</summary>
    public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>();
}
