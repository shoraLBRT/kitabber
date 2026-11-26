using Microsoft.EntityFrameworkCore;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Preferences.Contracts;

namespace Platform.Modules.Preferences.Blocks;

/// <summary>
/// The black lists over the schema <c>preferences</c> (ADR-0091): for other modules (<see cref="IBlockList"/>) and for
/// the feeds of the Posts module, which leave out the posts of the accounts the reader blocked
/// (<see cref="IFeedExclusionSource"/>, ADR-0089).
/// </summary>
public sealed class BlockList(PreferencesDbContext db) : IBlockList, IFeedExclusionSource
{
    public async Task<IReadOnlySet<Guid>> BlockedByAsync(Guid accountId, CancellationToken cancellationToken) =>
        (await db.BlockedAccounts.AsNoTracking()
            .Where(b => b.OwnerAccountId == accountId)
            .Select(b => b.AccountId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public Task<bool> IsBlockedAsync(Guid ownerAccountId, Guid accountId, CancellationToken cancellationToken) =>
        db.BlockedAccounts.AsNoTracking()
            .AnyAsync(b => b.OwnerAccountId == ownerAccountId && b.AccountId == accountId, cancellationToken);

    public async Task<FeedExclusions> ForReaderAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var blocked = await BlockedByAsync(accountId, cancellationToken);
        return blocked.Count == 0 ? FeedExclusions.None : new FeedExclusions([], [], [.. blocked]);
    }
}
