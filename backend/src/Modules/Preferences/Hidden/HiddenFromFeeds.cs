using Microsoft.EntityFrameworkCore;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Preferences.Hidden;

/// <summary>
/// What a reader hid, for the feeds of the Posts module (<see cref="IFeedExclusionSource"/>, ADR-0089, ADR-0119): the
/// hidden posts and the hidden topics, read from the schema <c>preferences</c>.
/// </summary>
public sealed class HiddenFromFeeds(PreferencesDbContext db) : IFeedExclusionSource
{
    public async Task<FeedExclusions> ForReaderAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var posts = await db.HiddenPosts.AsNoTracking()
            .Where(h => h.OwnerAccountId == accountId)
            .Select(h => h.PostId)
            .ToListAsync(cancellationToken);
        var topics = await db.HiddenTopics.AsNoTracking()
            .Where(h => h.OwnerAccountId == accountId)
            .Select(h => h.BlogId)
            .ToListAsync(cancellationToken);
        return posts.Count == 0 && topics.Count == 0 ? FeedExclusions.None : new FeedExclusions(posts, topics, []);
    }
}
