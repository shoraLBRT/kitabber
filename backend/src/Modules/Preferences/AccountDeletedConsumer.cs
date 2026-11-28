using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Messaging;
using Platform.Modules.Identity.Contracts;

namespace Platform.Modules.Preferences;

/// <summary>
/// Erases the settings of a deleted account (FR-ID-5, ADR-0087). The removal and the inbox record are saved together
/// after it returns (<see cref="AccountDeletedConsumerDefinition"/>); a repeat, or an account that never changed its
/// settings, finds no row and does nothing.
/// </summary>
public sealed class AccountDeletedConsumer(PreferencesDbContext db) : IConsumer<AccountDeleted>
{
    public async Task Consume(ConsumeContext<AccountDeleted> context)
    {
        var preferences = await db.AccountPreferences
            .SingleOrDefaultAsync(p => p.AccountId == context.Message.AccountId, context.CancellationToken);
        if (preferences is not null)
        {
            db.AccountPreferences.Remove(preferences);
        }

        // The account's own black list and its place in the lists of others (ADR-0091).
        var account = context.Message.AccountId;
        db.BlockedAccounts.RemoveRange(await db.BlockedAccounts
            .Where(b => b.OwnerAccountId == account || b.AccountId == account)
            .ToListAsync(context.CancellationToken));

        // The posts and topics the account hid (ADR-0119).
        db.HiddenPosts.RemoveRange(await db.HiddenPosts
            .Where(h => h.OwnerAccountId == account)
            .ToListAsync(context.CancellationToken));
        db.HiddenTopics.RemoveRange(await db.HiddenTopics
            .Where(h => h.OwnerAccountId == account)
            .ToListAsync(context.CancellationToken));
    }
}

/// <summary>The inbox of the Preferences schema for <see cref="AccountDeletedConsumer"/> (ADR-0005).</summary>
public sealed class AccountDeletedConsumerDefinition : ModuleConsumerDefinition<AccountDeletedConsumer, PreferencesDbContext>;
