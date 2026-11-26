using Microsoft.EntityFrameworkCore;
using Platform.Modules.Notifications.Contracts;

namespace Platform.Modules.Preferences.Blocks;

/// <summary>
/// The black list keeps a blocked account from notifying the person who blocked it (ADR-0091, ADR-0092): the
/// Notifications module asks it in the workers host before it writes a notification.
/// </summary>
public sealed class BlockedNotificationSuppression(PreferencesDbContext db) : INotificationSuppression
{
    public async Task<IReadOnlySet<Guid>> SuppressedActorsAsync(
        Guid recipientAccountId,
        IReadOnlyCollection<Guid> actorAccountIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actorAccountIds);
        return (await db.BlockedAccounts.AsNoTracking()
                .Where(b => b.OwnerAccountId == recipientAccountId && actorAccountIds.Contains(b.AccountId))
                .Select(b => b.AccountId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }
}
