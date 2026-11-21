using Platform.Modules.Media.Uploads;

namespace Platform.Modules.Media;

/// <summary>
/// An uploaded file (ADR-0030): its original in the object storage under <see cref="StorageKey"/>, bound to the
/// account that uploaded it. The owner is known only by the account_id (FR-ID-1). The type and the size are the ones
/// the content showed, not the ones the client declared.
/// </summary>
public sealed class MediaFile
{
    /// <summary>The longest original file name kept; a longer one is cut.</summary>
    public const int FileNameMaxLength = 255;

    public Guid Id { get; init; }

    /// <summary>The account that uploaded the file.</summary>
    public Guid OwnerAccountId { get; init; }

    public MediaKind Kind { get; init; }

    /// <summary>The MIME type recognised by content (<see cref="FileFormats"/>).</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>The size of the original in bytes.</summary>
    public long Size { get; init; }

    /// <summary>The key of the original in the storage: random, unrelated to anything else here (<see cref="Storage.StorageKeys"/>).</summary>
    public string StorageKey { get; init; } = string.Empty;

    /// <summary>The name the file had on the uploader's device, for downloads; never part of the key.</summary>
    public string? FileName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>A file of <paramref name="ownerAccountId"/>, saved under <paramref name="storageKey"/> at <paramref name="createdAt"/>.</summary>
    public static MediaFile Create(
        Guid ownerAccountId, FileFormat format, long size, string storageKey, string? fileName, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        if (ownerAccountId == Guid.Empty)
        {
            throw new ArgumentException("A media file needs its owner's account_id.", nameof(ownerAccountId));
        }

        // Only the last part of a path: some browsers send the full path from Windows, with backslashes.
        var name = Path.GetFileName(fileName?.Trim().Replace('\\', '/'));
        return new MediaFile
        {
            Id = Guid.CreateVersion7(createdAt),
            OwnerAccountId = ownerAccountId,
            Kind = format.Kind,
            ContentType = format.ContentType,
            Size = size,
            StorageKey = storageKey,
            FileName = string.IsNullOrEmpty(name) ? null : name[..Math.Min(name.Length, FileNameMaxLength)],
            CreatedAt = createdAt,
        };
    }
}
