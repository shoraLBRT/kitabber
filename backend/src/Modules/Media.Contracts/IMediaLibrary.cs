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

    /// <summary>
    /// Signed delivery links (ADR-0037) of the images among <paramref name="mediaIds"/> in <paramref name="variant"/>;
    /// an identifier that is not an uploaded image has no entry. A link does not expire: the file behind it never
    /// changes, so browsers and the CDN keep the answer for a year.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Uri>> ImageUrlsAsync(
        IReadOnlyCollection<Guid> mediaIds, ImageVariant variant, CancellationToken cancellationToken);
}

/// <summary>The format an image is delivered in (ADR-0037): always a modern one, whatever the original was.</summary>
public enum ImageFormat
{
    WebP = 1,
    Avif = 2,
}

/// <summary>
/// One delivered size and format of an image (ADR-0037). With a <see cref="Height"/> the picture is cut to exactly
/// <see cref="Width"/> by <see cref="Height"/> around its centre (an avatar, a cover); without one it is scaled to the
/// width and keeps its proportions. A picture is never enlarged beyond its original.
/// </summary>
/// <param name="Width">The width in pixels, from 1 to <see cref="MaxSide"/>.</param>
/// <param name="Height">The height in pixels, from 1 to <see cref="MaxSide"/>, or <c>null</c> to keep the proportions.</param>
/// <param name="Format">The format of the delivered file.</param>
public sealed record ImageVariant(int Width, int? Height, ImageFormat Format)
{
    /// <summary>The longest side a variant may ask for.</summary>
    public const int MaxSide = 4096;

    /// <summary>A square of <paramref name="side"/> pixels cut around the centre.</summary>
    public static ImageVariant Square(int side, ImageFormat format) => new(side, side, format);

    /// <summary>The whole picture scaled to <paramref name="width"/> pixels wide.</summary>
    public static ImageVariant ToWidth(int width, ImageFormat format) => new(width, null, format);
}
