using Microsoft.EntityFrameworkCore;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Media;

/// <summary><see cref="IMediaLibrary"/> over the schema <c>media</c> (ADR-0034).</summary>
internal sealed class MediaLibrary(MediaDbContext db) : IMediaLibrary
{
    public async Task<MediaFileSummary?> FindAsync(Guid mediaId, CancellationToken cancellationToken) =>
        await db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == mediaId, cancellationToken) is { } file
            ? new MediaFileSummary(file.Id, file.OwnerAccountId, (MediaFileKind)(int)file.Kind, file.ContentType)
            : null;
}
