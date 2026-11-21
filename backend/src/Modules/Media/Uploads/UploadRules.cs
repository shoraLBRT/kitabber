namespace Platform.Modules.Media.Uploads;

/// <summary>The largest upload of each kind, in bytes (<c>Media:MaxBytes:*</c>, docs/CONFIG.md).</summary>
public sealed record MediaLimits(long Image, long Video, long Audio, long File)
{
    public const long Megabyte = 1024 * 1024;

    /// <summary>The limits when the settings leave them out.</summary>
    public static MediaLimits Default { get; } = new(20 * Megabyte, 500 * Megabyte, 100 * Megabyte, 50 * Megabyte);

    /// <summary>The largest of the limits: the most a request body may carry.</summary>
    public long Largest => Math.Max(Math.Max(Image, Video), Math.Max(Audio, File));

    public long For(MediaKind kind) => kind switch
    {
        MediaKind.Image => Image,
        MediaKind.Video => Video,
        MediaKind.Audio => Audio,
        _ => File,
    };
}

/// <summary>Why an upload is refused.</summary>
public enum UploadRefusal
{
    /// <summary>No file, or an empty one.</summary>
    Empty,

    /// <summary>The content matches no accepted format, whatever the name says.</summary>
    UnsupportedType,

    /// <summary>The content is an accepted format, but the name's extension belongs to another one.</summary>
    ExtensionMismatch,

    /// <summary>The file is larger than the limit of its kind.</summary>
    TooLarge,
}

/// <summary>The verdict on an upload: the recognised format, or the reason it is refused.</summary>
public sealed record UploadCheck(FileFormat? Format, UploadRefusal? Refusal, long Limit = 0)
{
    public bool Accepted => Refusal is null;
}

/// <summary>
/// The rules of an upload (FR-MEDIA, ADR-0030): the content decides the format; an extension, when the name has one,
/// must be one of that format's extensions (a renamed file is refused, not silently fixed); the size must fit the
/// limit of the format's kind.
/// </summary>
public static class UploadRules
{
    /// <summary>Checks a file by its name, its first <see cref="FileFormats.HeaderLength"/> bytes and its full size.</summary>
    public static UploadCheck Check(string? fileName, ReadOnlySpan<byte> header, long size, MediaLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (size <= 0 || header.IsEmpty)
        {
            return new UploadCheck(null, UploadRefusal.Empty);
        }

        if (FileFormats.Detect(header) is not { } format)
        {
            return new UploadCheck(null, UploadRefusal.UnsupportedType);
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (extension.Length > 1 && !FileFormats.HasExtension(format, extension))
        {
            return new UploadCheck(format, UploadRefusal.ExtensionMismatch);
        }

        var limit = limits.For(format.Kind);
        return size > limit ? new UploadCheck(format, UploadRefusal.TooLarge, limit) : new UploadCheck(format, null, limit);
    }
}
