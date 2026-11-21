namespace Platform.Modules.Media.Contracts;

/// <summary>What an uploaded file is, as other modules see it (ADR-0030).</summary>
public enum MediaFileKind
{
    Image = 1,
    Video = 2,
    Audio = 3,

    /// <summary>An attached file that is neither an image, a video nor audio.</summary>
    File = 4,
}

/// <summary>An uploaded file as other modules see it; the storage key never leaves the Media module.</summary>
/// <param name="Id">The file's identifier, as <c>POST /api/v1/media</c> returned it.</param>
/// <param name="OwnerAccountId">The account that uploaded it.</param>
/// <param name="Kind">Its kind, recognised by content.</param>
/// <param name="ContentType">Its MIME type, recognised by content.</param>
public sealed record MediaFileSummary(Guid Id, Guid OwnerAccountId, MediaFileKind Kind, string ContentType);

/// <summary>
/// The uploaded files other modules may ask about (ADR-0034): the Media module implements it over its own tables, so
/// the caller never reads the schema <c>media</c>.
/// </summary>
public interface IMediaLibrary
{
    /// <summary>The file <paramref name="mediaId"/>, or <c>null</c> when there is none.</summary>
    Task<MediaFileSummary?> FindAsync(Guid mediaId, CancellationToken cancellationToken);
}
