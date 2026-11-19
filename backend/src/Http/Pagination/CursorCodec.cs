using System.Buffers.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Platform.Http.Problems;

namespace Platform.Http.Pagination;

/// <summary>
/// Encodes the position of a page as an opaque cursor: the keyset values of the last item, serialized as JSON
/// and written as base64url (ADR-0006). Clients never parse a cursor; the server may change its contents
/// without a new API version, because an old cursor that no longer decodes is just <c>invalid_cursor</c>.
/// </summary>
public static class CursorCodec
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public static string Encode<TCursor>(TCursor position)
        where TCursor : notnull =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(position, _options));

    /// <exception cref="ApiProblemException">The cursor is not one this server issued for this list.</exception>
    public static TCursor Decode<TCursor>(string cursor)
        where TCursor : notnull
    {
        try
        {
            var bytes = Base64Url.DecodeFromChars(cursor);
            return JsonSerializer.Deserialize<TCursor>(bytes, _options) ?? throw Invalid();
        }
        catch (Exception exception) when (exception is FormatException or JsonException or NotSupportedException)
        {
            throw Invalid();
        }
    }

    private static ApiProblemException Invalid() =>
        new(StatusCodes.Status400BadRequest, ApiErrorCodes.InvalidCursor, "Invalid cursor",
            "The cursor is malformed or belongs to another list. Start again without a cursor.");
}
