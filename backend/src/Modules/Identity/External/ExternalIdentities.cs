using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Platform.Modules.Identity.External;

/// <summary>How <see cref="ExternalIdentities.LinkAsync"/> ended.</summary>
public enum LinkOutcome
{
    /// <summary>The pair is now linked to the account.</summary>
    Linked,

    /// <summary>The pair was already linked to this account: nothing changed, nothing was written to the journal.</summary>
    AlreadyLinked,

    /// <summary>The pair belongs to another account; it is not moved (ADR-0098, п. 3).</summary>
    TakenByAnotherAccount,

    /// <summary>The account already has another identity of this provider; it is unlinked first.</summary>
    ProviderAlreadyLinked,

    AccountNotFound,
}

/// <summary>How <see cref="ExternalIdentities.UnlinkAsync"/> ended.</summary>
public enum UnlinkOutcome
{
    Unlinked,

    /// <summary>The account has no such identity (or it is another account's: whose stays unknown).</summary>
    NotFound,

    /// <summary>Without this identity the account could not sign in at all (ADR-0098, п. 5).</summary>
    LastSignInMethod,
}

/// <param name="Id">The link's identifier, the one to unlink it by.</param>
/// <param name="Provider">The provider's key, <see cref="ExternalProviders"/>.</param>
public sealed record ExternalIdentityView(Guid Id, string Provider, DateTimeOffset LinkedAt);

/// <summary>
/// The external identities of accounts (FR-ID-4, ADR-0098): linking a pair «provider, subject», finding the account of a
/// pair at sign-in, listing and unlinking. Every link and unlink is written to the journal in the same save. Proving that
/// the person really is that subject is the job of the provider's sign-in (#91), behind its own interface; this class
/// trusts its caller.
/// </summary>
public sealed class ExternalIdentities(IdentityDbContext db, TimeProvider time)
{
    /// <summary>Links the pair to the account (ADR-0098, п. 2–3).</summary>
    public async Task<LinkOutcome> LinkAsync(
        Guid accountId, string provider, string subject, CancellationToken cancellationToken)
    {
        Validate(provider, subject);
        if (!await db.Accounts.AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            return LinkOutcome.AccountNotFound;
        }

        var existing = await db.ExternalIdentities.AsNoTracking()
            .Where(i => (i.Provider == provider && i.Subject == subject)
                || (i.AccountId == accountId && i.Provider == provider))
            .ToListAsync(cancellationToken);
        if (Outcome(existing, accountId, subject) is { } refused)
        {
            return refused;
        }

        var identity = ExternalIdentity.Link(accountId, provider, subject, time.GetUtcNow());
        db.ExternalIdentities.Add(identity);
        db.ExternalIdentityLog.Add(ExternalIdentityLogEntry.Of(identity, ExternalIdentityAction.Linked, identity.LinkedAt));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return LinkOutcome.Linked;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        })
        {
            // Another request linked the same pair, or the same provider, at the same moment: answer as it would have.
            db.ChangeTracker.Clear();
            existing = await db.ExternalIdentities.AsNoTracking()
                .Where(i => (i.Provider == provider && i.Subject == subject)
                    || (i.AccountId == accountId && i.Provider == provider))
                .ToListAsync(cancellationToken);
            return Outcome(existing, accountId, subject) ?? throw new InvalidOperationException(
                "A link was refused by a unique index that no stored link explains.", e);
        }
    }

    /// <summary>
    /// Adds the link of a new account, not saved yet, to the context with its journal row: the account and its first
    /// identity are saved together (ADR-0115).
    /// </summary>
    internal void LinkToNew(Account account, string provider, string subject)
    {
        Validate(provider, subject);
        var identity = ExternalIdentity.Link(account.Id, provider, subject, time.GetUtcNow());
        db.ExternalIdentities.Add(identity);
        db.ExternalIdentityLog.Add(ExternalIdentityLogEntry.Of(identity, ExternalIdentityAction.Linked, identity.LinkedAt));
    }

    /// <summary>The account the pair is linked to, for signing in with the provider; null when it is linked to none.</summary>
    public async Task<Guid?> FindAccountAsync(string provider, string subject, CancellationToken cancellationToken)
    {
        Validate(provider, subject);
        return await db.ExternalIdentities.AsNoTracking()
            .Where(i => i.Provider == provider && i.Subject == subject)
            .Select(i => (Guid?)i.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>The account's identities, in the order they were linked.</summary>
    public async Task<ExternalIdentityView[]> ListAsync(Guid accountId, CancellationToken cancellationToken) =>
        await db.ExternalIdentities.AsNoTracking()
            .Where(i => i.AccountId == accountId)
            .OrderBy(i => i.LinkedAt).ThenBy(i => i.Id)
            .Select(i => new ExternalIdentityView(i.Id, i.Provider, i.LinkedAt))
            .ToArrayAsync(cancellationToken);

    /// <summary>
    /// Unlinks one identity of the account, unless it is the account's last way to sign in (ADR-0098, п. 5). The account
    /// row is locked for the check, so two unlinks of the last two identities cannot both pass.
    /// </summary>
    public async Task<UnlinkOutcome> UnlinkAsync(Guid accountId, Guid identityId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.accounts WHERE id = {accountId} FOR UPDATE", cancellationToken);
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        var identity = account is null
            ? null
            : await db.ExternalIdentities.SingleOrDefaultAsync(
                i => i.Id == identityId && i.AccountId == accountId, cancellationToken);
        if (account is null || identity is null)
        {
            return UnlinkOutcome.NotFound;
        }

        var others = await db.ExternalIdentities.CountAsync(
            i => i.AccountId == accountId && i.Id != identityId, cancellationToken);
        if (!SignInMethods.RemainAfterUnlink(account, others))
        {
            return UnlinkOutcome.LastSignInMethod;
        }

        db.ExternalIdentities.Remove(identity);
        db.ExternalIdentityLog.Add(
            ExternalIdentityLogEntry.Of(identity, ExternalIdentityAction.Unlinked, time.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UnlinkOutcome.Unlinked;
    }

    private static LinkOutcome? Outcome(List<ExternalIdentity> existing, Guid accountId, string subject)
    {
        if (existing.Find(i => i.Subject == subject) is { } pair)
        {
            return pair.AccountId == accountId ? LinkOutcome.AlreadyLinked : LinkOutcome.TakenByAnotherAccount;
        }

        return existing.Count > 0 ? LinkOutcome.ProviderAlreadyLinked : null;
    }

    private static void Validate(string provider, string subject)
    {
        if (!ExternalProviders.IsValid(provider))
        {
            throw new ArgumentException($"'{provider}' is not a provider key.", nameof(provider));
        }

        if (!ExternalProviders.IsValidSubject(subject))
        {
            throw new ArgumentException("The subject is blank, padded with spaces or too long.", nameof(subject));
        }
    }
}

/// <summary>
/// The ways an account can sign in (ADR-0098, п. 5). The password and the one-time code both go by the email and both
/// need it confirmed (ADR-0020, ADR-0022), so a confirmed email is one way; each linked external identity is another.
/// </summary>
public static class SignInMethods
{
    /// <summary>Whether the account can still sign in with <paramref name="otherIdentities"/> identities left.</summary>
    public static bool RemainAfterUnlink(Account account, int otherIdentities)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentOutOfRangeException.ThrowIfNegative(otherIdentities);
        return account.HasConfirmedEmail || otherIdentities > 0;
    }
}
