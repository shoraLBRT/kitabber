using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Platform.Http.Problems;

/// <summary>
/// Turns exceptions into problem details: <see cref="ApiProblemException"/> keeps its status and code,
/// <see cref="BadHttpRequestException"/> (unreadable body, bad parameter) becomes <c>bad_request</c>,
/// anything else is logged and becomes a 500 <c>internal_error</c> without the exception text.
/// </summary>
internal sealed partial class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case ApiProblemException api:
                problem = FromApiProblem(api);
                break;
            case BadHttpRequestException bad:
                problem = new ProblemDetails { Status = bad.StatusCode, Title = "Bad request", Detail = bad.Message };
                break;
            default:
                LogUnhandled(logger, exception, httpContext.Request.Path);
                problem = new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "Internal error" };
                problem.Extensions[ApiProblems.CodeKey] = ApiErrorCodes.InternalError;
                break;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails FromApiProblem(ApiProblemException exception)
    {
        var problem = new ProblemDetails
        {
            Status = exception.Status,
            Title = exception.Title,
            Detail = exception.Detail,
        };
        foreach (var (key, value) in exception.Extensions)
        {
            problem.Extensions[key] = value;
        }

        problem.Extensions[ApiProblems.CodeKey] = exception.Code;
        return problem;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, PathString path);
}
