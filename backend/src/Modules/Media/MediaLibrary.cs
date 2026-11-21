using Microsoft.EntityFrameworkCore;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Media.Images;
using Platform.Modules.Media.Uploads;

namespace Platform.Modules.Media;

/// <summary><see cref="IMediaLibrary"/> over the schema <c>media</c> (ADR-0034, ADR-0037).</summary>
internal sealed class MediaLibrary(MediaDbContext db, ImgproxyUrls images) : IMediaLibrary
{
    public async Task<MediaFileSummary?> FindAsync(Guid mediaId, CancellationToken cancellationToken) =>
        await db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == mediaId, cancellationToken) is { } file
            ? new MediaFileSummary(file.Id, file.OwnerAccountId, (MediaFileKind)(int)file.Kind, file.ContentType)
            : null;

    public async Task<IReadOnlyDictionary<Guid, Uri>> ImageUrlsAsync(
        IReadOnlyCollection<Guid> mediaIds, ImageVariant variant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediaIds);
        ArgumentNullException.ThrowIfNull(variant);
        if (mediaIds.Count == 0)
        {
            return new Dictionary<Guid, Uri>();
        }

        var ids = mediaIds.Distinct().ToList();
        var keys = await db.Files.AsNoTracking()
            .Where(f => ids.Contains(f.Id) && f.Kind == MediaKind.Image)
            .Select(f => new { f.Id, f.StorageKey })
            .ToListAsync(cancellationToken);
        return keys.ToDictionary(f => f.Id, f => images.Build(f.StorageKey, variant));
    }
}
