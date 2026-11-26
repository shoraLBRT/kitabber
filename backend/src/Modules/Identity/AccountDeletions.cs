using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Platform.Modules.Identity;

/// <summary>
/// Deletes an account (FR-ID-5, ADR-0087): its row goes with its email confirmation and its device sessions, and
/// <see cref="Contracts.AccountDeleted"/> goes into the module's outbox in the same save, so every module erases what it
/// keeps by the account_id. The provider refuses the account's earlier codes and refresh tokens once it is gone
/// (<c>AuthorizationEndpoint</c>). How a person asks for it, the waiting period and the restore are E03-11 (#170).
/// </summary>
public sealed class AccountDeletions(IdentityDbContext db, IPublishEndpoint publish, TimeProvider time)
{
    /// <summary>Deletes the account; false when there is no such account.</summary>
    public async Task<bool> DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return false;
        }

        db.Accounts.Remove(account);
        await AccountEventPublisher.PublishAsync(db, publish, time, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
