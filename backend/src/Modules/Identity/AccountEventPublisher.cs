using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Platform.Messaging;
using Platform.Modules.Identity.Contracts;

namespace Platform.Modules.Identity;

/// <summary>
/// The account events of one save (FR-ID-5, ADR-0087), read from the accounts the context is about to save: they go into
/// the module's outbox before <c>SaveChanges</c> and are saved, or dropped, together with the change. Only an account
/// that is here to stay (ADR-0025) is announced: a registration waiting for its code is created and deleted silently.
/// </summary>
internal static class AccountEventPublisher
{
    public static async Task PublishAsync(
        IdentityDbContext db,
        IModuleOutbox<IdentityDbContext> publish,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        foreach (var message in Collect(db, time))
        {
            await publish.Publish(message, message.GetType(), cancellationToken);
        }
    }

    // In the order a reader expects: created before its email, an update before the email that caused it.
    private static List<object> Collect(IdentityDbContext db, TimeProvider time)
    {
        var messages = new List<object>();
        foreach (var entry in db.ChangeTracker.Entries<Account>().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity.EmailConfirmedAt is { } confirmedAt:
                    AddCreated(messages, entry.Entity.Id, confirmedAt);
                    break;
                case EntityState.Modified:
                    AddModified(messages, entry);
                    break;
                case EntityState.Deleted when Original(entry, a => a.EmailConfirmedAt) is not null:
                    messages.Add(new AccountDeleted(entry.Entity.Id, time.GetUtcNow()));
                    break;
            }
        }

        return messages;
    }

    private static void AddModified(List<object> messages, EntityEntry<Account> entry)
    {
        if (entry.Entity.EmailConfirmedAt is not { } confirmedAt)
        {
            return;
        }

        var wasConfirmedAt = Original(entry, a => a.EmailConfirmedAt);
        if (wasConfirmedAt is null)
        {
            AddCreated(messages, entry.Entity.Id, confirmedAt);
        }
        else if (confirmedAt != wasConfirmedAt)
        {
            if (!string.Equals(Original(entry, a => a.Email), entry.Entity.Email, StringComparison.Ordinal))
            {
                messages.Add(new AccountUpdated(entry.Entity.Id, confirmedAt, [AccountChanges.Email]));
            }

            messages.Add(new AccountEmailVerified(entry.Entity.Id, confirmedAt));
        }
    }

    // AccountActivated stays for the modules that already react to it (ADR-0025); account.created is the contract.
    private static void AddCreated(List<object> messages, Guid accountId, DateTimeOffset confirmedAt)
    {
        messages.Add(new AccountActivated(accountId, confirmedAt));
        messages.Add(new AccountCreated(accountId, confirmedAt));
        messages.Add(new AccountEmailVerified(accountId, confirmedAt));
    }

    private static T Original<T>(EntityEntry<Account> entry, System.Linq.Expressions.Expression<Func<Account, T>> property) =>
        entry.Property(property).OriginalValue;
}
