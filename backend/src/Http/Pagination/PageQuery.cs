using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Platform.Http.Problems;

namespace Platform.Http.Pagination;

/// <summary>
/// The query string of a paginated list: <c>?cursor=…&amp;limit=…</c>. Bind it with <c>[AsParameters]</c> and turn
/// it into a request with <see cref="ToRequest{TCursor}"/>.
/// </summary>
/// <param name="Cursor">The <c>nextCursor</c> of the previous page; absent for the first page.</param>
/// <param name="Limit">Items per page, 1 to <see cref="MaxLimit"/>; <see cref="DefaultLimit"/> when absent.</param>
public sealed record PageQuery(
    [FromQuery(Name = "cursor")] string? Cursor = null,
    [FromQuery(Name = "limit")] int? Limit = null)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    /// <exception cref="ApiProblemException">
    /// <c>validation_failed</c> for a limit out of range, <c>invalid_cursor</c> for a cursor that does not decode.
    /// </exception>
    public PageRequest<TCursor> ToRequest<TCursor>()
        where TCursor : notnull
    {
        var limit = Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            var problem = new ApiProblemException(
                StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed, "Validation failed");
            problem.Extensions["errors"] =
                new Dictionary<string, string[]> { ["limit"] = [$"The limit must be between 1 and {MaxLimit}."] };
            throw problem;
        }

        var after = string.IsNullOrEmpty(Cursor) ? default : CursorCodec.Decode<TCursor>(Cursor);
        return new PageRequest<TCursor>(after, limit);
    }
}
