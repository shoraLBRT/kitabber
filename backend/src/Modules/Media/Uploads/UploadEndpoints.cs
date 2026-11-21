using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Platform.Http;
using Platform.Http.Problems;

namespace Platform.Modules.Media.Uploads;

/// <summary>An uploaded file as the API returns it.</summary>
/// <param name="Id">The file's identifier: posts and comments refer to it.</param>
/// <param name="Kind"><c>image</c>, <c>video</c>, <c>audio</c> or <c>file</c>.</param>
/// <param name="ContentType">The MIME type recognised by content.</param>
/// <param name="Size">The size in bytes.</param>
/// <param name="FileName">The name the file had on the device, or <c>null</c>.</param>
/// <param name="CreatedAt">When it was uploaded.</param>
public sealed record UploadedMedia(Guid Id, string Kind, string ContentType, long Size, string? FileName, DateTimeOffset CreatedAt);

/// <summary>
/// <c>POST /api/v1/media</c> (FR-MEDIA, ADR-0030): a signed-in account uploads one file as <c>multipart/form-data</c>
/// in the field <c>file</c>. The content decides the type; the limits of <c>Media:MaxBytes:*</c> the size.
/// </summary>
public sealed class UploadEndpoints(MediaLimits limits) : IApiEndpoints
{
    /// <summary>The content matches no accepted format (415).</summary>
    public const string UnsupportedTypeCode = "media_type_unsupported";

    /// <summary>The name's extension belongs to another format than the content (415).</summary>
    public const string ExtensionMismatchCode = "media_extension_mismatch";

    /// <summary>The file is larger than the limit of its kind (413).</summary>
    public const string TooLargeCode = "media_too_large";

    // Room for the multipart boundaries and part headers around the largest file.
    private const long _multipartOverhead = 64 * 1024;

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var bodyLimit = limits.Largest + _multipartOverhead;
        v1.MapPost("/media", UploadAsync)
            .WithTags("Media")
            .RequireAuthorization()
            // The API signs requests in by bearer token (ADR-0018), never by a cookie alone: no CSRF to guard against.
            .DisableAntiforgery()
            .WithMetadata(new RequestBodyLimit(bodyLimit))
            .WithFormOptions(multipartBodyLengthLimit: bodyLimit)
            .Produces<UploadedMedia>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .WithName("UploadMedia")
            .WithSummary("Upload a file: an image, a video, audio or a PDF, checked by its content");
    }

    private static async Task<IResult> UploadAsync(
        [FromForm] IFormFile? file, ClaimsPrincipal user, MediaUploads uploads, CancellationToken ct)
    {
        if (AccountOf(user) is not { } accountId)
        {
            return TypedResults.Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return EmptyFile();
        }

        await using var content = file.OpenReadStream();
        var result = await uploads.UploadAsync(accountId, file.FileName, content, file.Length, ct);
        if (result.File is { } saved)
        {
            return TypedResults.Created((string?)null, View(saved));
        }

        var check = result.Check;
        return check.Refusal switch
        {
            UploadRefusal.TooLarge => ApiProblems.Problem(
                StatusCodes.Status413PayloadTooLarge,
                TooLargeCode,
                "Payload too large",
                string.Create(CultureInfo.InvariantCulture, $"Файл больше {MegabytesOf(check.Limit)} МБ — предела для этого типа.")),
            UploadRefusal.ExtensionMismatch => ApiProblems.Problem(
                StatusCodes.Status415UnsupportedMediaType,
                ExtensionMismatchCode,
                "Unsupported media type",
                $"Расширение файла не совпадает с содержимым: это {check.Format!.Extensions[0].ToUpperInvariant()}."),
            UploadRefusal.UnsupportedType => ApiProblems.Problem(
                StatusCodes.Status415UnsupportedMediaType,
                UnsupportedTypeCode,
                "Unsupported media type",
                "Такой тип файла не поддерживается: JPEG, PNG, WebP, GIF, MP4, MOV, WebM, MP3, M4A, OGG, WAV, FLAC или PDF."),
            _ => EmptyFile(),
        };
    }

    private static IResult EmptyFile() => ApiProblems.Validation(new Dictionary<string, string[]>
    {
        ["file"] = ["Выберите непустой файл."],
    });

    private static UploadedMedia View(MediaFile file) => new(
        file.Id,
        file.Kind.ToString().ToLowerInvariant(),
        file.ContentType,
        file.Size,
        file.FileName,
        file.CreatedAt);

    private static string MegabytesOf(long bytes) =>
        Math.Round(bytes / (double)MediaLimits.Megabyte, 1).ToString("0.#", CultureInfo.InvariantCulture);

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;

    private sealed class RequestBodyLimit(long bytes) : IRequestSizeLimitMetadata
    {
        public long? MaxRequestBodySize => bytes;
    }
}
