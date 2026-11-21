using Microsoft.Extensions.Logging;
using Platform.Modules.Media.Storage;

namespace Platform.Modules.Media.Uploads;

/// <summary>The result of <see cref="MediaUploads.UploadAsync"/>: the saved file, or the check that refused it.</summary>
public sealed record UploadResult(MediaFile? File, UploadCheck Check);

/// <summary>
/// Accepting an upload (ADR-0030): check it by <see cref="UploadRules"/>, put the original into the storage under a new
/// random key, then record it with its owner. When the record cannot be saved, the object is removed again.
/// </summary>
public sealed partial class MediaUploads(
    MediaDbContext db, IObjectStorage storage, MediaLimits limits, TimeProvider time, ILogger<MediaUploads> logger)
{
    /// <summary>
    /// Uploads <paramref name="content"/> (a readable, seekable stream of <paramref name="size"/> bytes) for
    /// <paramref name="ownerAccountId"/>.
    /// </summary>
    public async Task<UploadResult> UploadAsync(
        Guid ownerAccountId, string? fileName, Stream content, long size, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var header = new byte[FileFormats.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var check = UploadRules.Check(fileName, header.AsSpan(0, read), size, limits);
        if (!check.Accepted)
        {
            return new UploadResult(null, check);
        }

        content.Position = 0;
        var key = StorageKeys.NewOriginal();
        await storage.PutAsync(key, content, check.Format!.ContentType, cancellationToken);
        var file = MediaFile.Create(ownerAccountId, check.Format, size, key, fileName, time.GetUtcNow());
        db.Files.Add(file);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await RemoveOrphanAsync(key);
            throw;
        }

        return new UploadResult(file, check);
    }

    private async Task RemoveOrphanAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
#pragma warning disable CA1031 // The original failure is the one to report; a leftover object is only logged.
        catch (Exception e)
#pragma warning restore CA1031
        {
            LogOrphan(logger, e, key);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The original {StorageKey} was stored but not recorded and could not be removed")]
    private static partial void LogOrphan(ILogger logger, Exception exception, string storageKey);
}
