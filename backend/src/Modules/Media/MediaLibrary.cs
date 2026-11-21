using Microsoft.EntityFrameworkCore;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Media.Images;
using Platform.Modules.Media.Uploads;

namespace Platform.Modules.Media;

/// <summary><see cref="IMediaLibrary"/> over the schema <c>media</c> (ADR-0034, ADR-0037, ADR-0040).</summary>
internal sealed class MediaLibrary(MediaDbContext db, ImgproxyUrls images) : IMediaLibrary
{
    public async Task<MediaFileSummary?> FindAsync(Guid mediaId, CancellationToken cancellationToken) =>
        await db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == mediaId, cancellationToken) is { } file
            ? new MediaFileSummary(file.Id, file.OwnerAccountId, (MediaFileKind)(int)file.Kind, file.ContentType)
            : null;

    public async Task<IReadOnlyDictionary<Guid, Uri>> ImageUrlsAsync(
        IReadOnlyCollection<Guid> mediaIds, ImageVariant variant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variant);
        var keys = await StorageKeysAsync(mediaIds, cancellationToken);
        return keys.ToDictionary(f => f.Key, f => images.Build(f.Value, variant));
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ImageSource>>> ImageSourcesAsync(
        IReadOnlyCollection<Guid> mediaIds, IReadOnlyList<ImageVariant> variants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variants);
        var keys = await StorageKeysAsync(mediaIds, cancellationToken);
        return keys.ToDictionary(
            f => f.Key,
            f => (IReadOnlyList<ImageSource>)[.. variants.Select(v => new ImageSource(images.Build(f.Value, v), v.Width))]);
    }

    // The storage keys of the images among the identifiers: one query for a whole feed.
    private async Task<Dictionary<Guid, string>> StorageKeysAsync(
        IReadOnlyCollection<Guid> mediaIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediaIds);
        if (mediaIds.Count == 0)
        {
            return [];
        }

        var ids = mediaIds.Distinct().ToList();
        return await db.Files.AsNoTracking()
            .Where(f => ids.Contains(f.Id) && f.Kind == MediaKind.Image)
            .ToDictionaryAsync(f => f.Id, f => f.StorageKey, cancellationToken);
    }
}
