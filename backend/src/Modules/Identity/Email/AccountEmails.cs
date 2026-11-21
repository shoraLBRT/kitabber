using System.Globalization;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Mail;
using Platform.Modules.Identity.Contracts;

namespace Platform.Modules.Identity.Email;

/// <summary>What <see cref="AccountEmails.ChangeAsync"/> did.</summary>
public enum EmailChangeResult
{
    /// <summary>A confirmation code went to the address.</summary>
    CodeSent,

    /// <summary>The address is already the account's confirmed email: nothing to do.</summary>
    AlreadyConfirmed,

    /// <summary>Not an address the platform accepts.</summary>
    Invalid,

    /// <summary>Another account has the same normalised address.</summary>
    Taken,

    /// <summary>No such account.</summary>
    NoAccount,
}

/// <summary>What <see cref="AccountEmails.ConfirmAsync"/> did.</summary>
public enum EmailConfirmResult
{
    Confirmed,

    /// <summary>No code was requested, or the code is wrong, expired or spent.</summary>
    Invalid,

    /// <summary>Another account confirmed the same normalised address first.</summary>
    Taken,
}

/// <summary>
/// The account's email (FR-ID, ADR-0020): setting it sends a confirmation code, the code confirms it. While an account
/// has no confirmed email, a new address replaces the account's email at once, unconfirmed; once it has one, a new
/// address waits in the confirmation and replaces the confirmed one only when its code is entered, so the account never
/// loses its confirmed email (and the right to publish) halfway.
/// </summary>
public sealed class AccountEmails(IdentityDbContext db, IMailer mailer, IPublishEndpoint publish, TimeProvider time)
{
    public const string Template = "email-confirmation";

    private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru");

    /// <summary>Sets <paramref name="email"/> for the account and sends it a confirmation code.</summary>
    public async Task<EmailChangeResult> ChangeAsync(Guid accountId, string? email, CancellationToken cancellationToken)
    {
        var normalized = EmailAddresses.Normalize(email);
        if (normalized is null)
        {
            return EmailChangeResult.Invalid;
        }

        var typed = email!.Trim();
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return EmailChangeResult.NoAccount;
        }

        if (account.HasConfirmedEmail && account.NormalizedEmail == normalized)
        {
            return EmailChangeResult.AlreadyConfirmed;
        }

        var holder = await db.Accounts
            .Where(a => a.NormalizedEmail == normalized && a.Id != accountId)
            .Select(a => new { a.EmailConfirmedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (holder?.EmailConfirmedAt is not null)
        {
            return EmailChangeResult.Taken;
        }

        if (!account.HasConfirmedEmail)
        {
            // Another account holds the address unconfirmed (ADR-0022): the claim waits in the confirmation, and the
            // one who enters a code first gets the address.
            if (holder is null)
            {
                account.SetUnconfirmedEmail(typed, normalized);
            }
            else if (account.NormalizedEmail is not null)
            {
                account.ReleaseUnconfirmedEmail();
            }
        }

        var confirmation = await db.EmailConfirmations.SingleOrDefaultAsync(c => c.AccountId == accountId, cancellationToken);
        if (!await QueueCodeAsync(accountId, confirmation, typed, normalized, cancellationToken))
        {
            return EmailChangeResult.Invalid;
        }

        return await SaveAsync(cancellationToken) ? EmailChangeResult.CodeSent : EmailChangeResult.Taken;
    }

    /// <summary>
    /// Starts the confirmation of <paramref name="typed"/> for the account over <paramref name="confirmation"/> (or a new
    /// one) and queues the letter with the code; nothing is saved. False when the mail library cannot parse the address.
    /// </summary>
    internal async Task<bool> QueueCodeAsync(
        Guid accountId, EmailConfirmation? confirmation, string typed, string normalized, CancellationToken cancellationToken)
    {
        if (confirmation is null)
        {
            confirmation = EmailConfirmation.Create(accountId);
            db.EmailConfirmations.Add(confirmation);
        }

        var code = EmailConfirmation.NewCode();
        confirmation.Reset(typed, normalized, code, time.GetUtcNow());
        try
        {
            // Through the module's outbox: the letter leaves only if the code is saved, and is not lost if the broker is down.
            await mailer.SendAsync(typed, Template, _culture, new Dictionary<string, string>
            {
                ["code"] = code,
                ["lifetime"] = "30 минут",
            }, cancellationToken);
            return true;
        }
        catch (ArgumentException)
        {
            // An address the normaliser accepts but the mail library does not parse.
            return false;
        }
    }

    /// <summary>Confirms the address the account's last code was sent to.</summary>
    public async Task<EmailConfirmResult> ConfirmAsync(Guid accountId, string? code, CancellationToken cancellationToken)
    {
        var confirmation = await db.EmailConfirmations.SingleOrDefaultAsync(c => c.AccountId == accountId, cancellationToken);
        var now = time.GetUtcNow();
        if (confirmation is null || string.IsNullOrWhiteSpace(code) || !confirmation.IsUsable(now))
        {
            return EmailConfirmResult.Invalid;
        }

        if (!confirmation.Accepts(code))
        {
            await db.SaveChangesAsync(cancellationToken);
            return EmailConfirmResult.Invalid;
        }

        var account = await db.Accounts.SingleAsync(a => a.Id == accountId, cancellationToken);
        db.EmailConfirmations.Remove(confirmation);
        if (!await ReleaseUnconfirmedAsync(confirmation.NormalizedEmail, accountId, cancellationToken))
        {
            return EmailConfirmResult.Taken;
        }

        account.ConfirmEmail(confirmation.Email, confirmation.NormalizedEmail, now);
        return await SaveAsync(cancellationToken) ? EmailConfirmResult.Confirmed : EmailConfirmResult.Taken;
    }

    /// <summary>
    /// Takes <paramref name="normalized"/> away from the account other than <paramref name="claimant"/> that holds it
    /// unconfirmed (ADR-0022): a code from the mailbox has just proved whose the address is. A registration waiting for
    /// that address is deleted with its code, another account only loses the address. False when an account has the
    /// address confirmed; nothing is saved.
    /// </summary>
    internal async Task<bool> ReleaseUnconfirmedAsync(string normalized, Guid? claimant, CancellationToken cancellationToken)
    {
        var holder = await db.Accounts.SingleOrDefaultAsync(
            a => a.NormalizedEmail == normalized && a.Id != claimant, cancellationToken);
        if (holder is null)
        {
            return true;
        }

        if (holder.HasConfirmedEmail)
        {
            return false;
        }

        if (holder.IsPendingRegistration)
        {
            db.Accounts.Remove(holder);
        }
        else
        {
            holder.ReleaseUnconfirmedEmail();
        }

        // Saved apart from the claim, so the unique index never sees the address twice whatever order EF chooses.
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The account whose <b>confirmed</b> email normalises as <paramref name="email"/> does: the key a merge of accounts
    /// matches people by (FR-ID). An address that is not confirmed is never a match: anyone can type anyone's address.
    /// </summary>
    public async Task<Guid?> FindByConfirmedEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = EmailAddresses.Normalize(email);
        return normalized is null
            ? null
            : await db.Accounts
                .Where(a => a.NormalizedEmail == normalized && a.EmailConfirmedAt != null)
                .Select(a => (Guid?)a.Id)
                .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Whether the account exists and its email is confirmed.</summary>
    public Task<bool> IsConfirmedAsync(Guid accountId, CancellationToken cancellationToken) =>
        db.Accounts.AnyAsync(a => a.Id == accountId && a.EmailConfirmedAt != null, cancellationToken);

    // False when the unique index on the normalised email refused the change: another account got there first.
    internal async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        await PublishActivationsAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IdentityDbContext.NormalizedEmailIndex,
        })
        {
            return false;
        }
    }

    // An account whose email is confirmed for the first time in this save is activated (ADR-0023): the event goes into
    // the module's outbox and is saved, or dropped, together with the confirmation.
    private async Task PublishActivationsAsync(CancellationToken cancellationToken)
    {
        var activated = db.ChangeTracker.Entries<Account>()
            .Where(e => e.Entity.EmailConfirmedAt is not null
                && (e.State == EntityState.Added
                    || (e.State == EntityState.Modified && e.Property(a => a.EmailConfirmedAt).OriginalValue is null)))
            .Select(e => new AccountActivated(e.Entity.Id, e.Entity.EmailConfirmedAt!.Value))
            .ToList();
        foreach (var message in activated)
        {
            await publish.Publish(message, cancellationToken);
        }
    }
}
