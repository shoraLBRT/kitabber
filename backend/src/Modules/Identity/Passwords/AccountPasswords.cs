using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenIddict.Abstractions;
using Platform.Mail;
using Platform.Modules.Identity.Email;

namespace Platform.Modules.Identity.Passwords;

/// <summary>What <see cref="AccountPasswords.RequestResetAsync"/> did.</summary>
public enum PasswordResetRequestStatus
{
    /// <summary>The request was taken; a code went out if an account has the address.</summary>
    Requested,

    /// <summary>Not an address the platform accepts.</summary>
    InvalidEmail,

    /// <summary>The last request for the address was less than <see cref="PasswordReset.ResendInterval"/> ago.</summary>
    TooSoon,
}

/// <param name="Status">What happened.</param>
/// <param name="ResetId">The request the code belongs to, when it was taken.</param>
/// <param name="RetryAfter">How long until a new code may be requested.</param>
public sealed record PasswordResetRequestResult(PasswordResetRequestStatus Status, Guid? ResetId, TimeSpan RetryAfter);

/// <summary>What <see cref="AccountPasswords.ResetAsync"/> and <see cref="AccountPasswords.ChangeAsync"/> did.</summary>
public enum PasswordChangeStatus
{
    /// <summary>The account has the new password.</summary>
    Changed,

    /// <summary>The new password breaks the rules (<see cref="PasswordHasher.IsAcceptable"/>); nothing was spent.</summary>
    InvalidPassword,

    /// <summary>The code is wrong, expired or spent, or the current password is wrong.</summary>
    Refused,
}

/// <param name="Status">What happened.</param>
/// <param name="AccountId">The account whose password changed.</param>
public sealed record PasswordChangeResult(PasswordChangeStatus Status, Guid? AccountId = null);

/// <summary>
/// The account's password after registration (FR-AUTH, E03-05, ADR-0036): a reset by a one-time code from a letter,
/// which ends every earlier session of the account, and a change by the signed-in owner who knows the current one.
/// Both tell the owner by a letter. Asking for a reset does not tell whether an address is registered.
/// </summary>
public sealed class AccountPasswords(
    IdentityDbContext db,
    IMailer<IdentityDbContext> mailer,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens,
    SignIn.AccountSessions sessions,
    TimeProvider time)
{
    public const string ResetTemplate = "password-reset";
    public const string ChangedTemplate = "password-changed";

    private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru");

    /// <summary>
    /// Takes a request to reset the password of <paramref name="email"/>, once the resend timer of the last one allows.
    /// The code goes out only when an account has confirmed the address; the answer is the same either way.
    /// </summary>
    public async Task<PasswordResetRequestResult> RequestResetAsync(string? email, CancellationToken cancellationToken)
    {
        var normalized = EmailAddresses.Normalize(email);
        if (normalized is null)
        {
            return new PasswordResetRequestResult(PasswordResetRequestStatus.InvalidEmail, null, TimeSpan.Zero);
        }

        var now = time.GetUtcNow();
        await db.PasswordResets.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        var entry = await db.PasswordResets.SingleOrDefaultAsync(r => r.NormalizedEmail == normalized, cancellationToken);
        if (entry?.ResendDelay(now) is { } delay && delay > TimeSpan.Zero)
        {
            return new PasswordResetRequestResult(PasswordResetRequestStatus.TooSoon, null, delay);
        }

        if (entry is null)
        {
            entry = PasswordReset.Create(normalized);
            db.PasswordResets.Add(entry);
        }

        var account = await db.Accounts.AsNoTracking()
            .Where(a => a.NormalizedEmail == normalized && a.EmailConfirmedAt != null)
            .Select(a => new { a.Id, a.Email })
            .SingleOrDefaultAsync(cancellationToken);
        var code = EmailConfirmation.NewCode();
        entry.Reset(account?.Id, code, now);
        if (account is not null)
        {
            try
            {
                // Through the module's outbox: the letter leaves only if the request is saved.
                await mailer.SendAsync(account.Email!, ResetTemplate, _culture, new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["lifetime"] = PasswordReset.LifetimeText,
                }, cancellationToken);
            }
            catch (ArgumentException)
            {
                return new PasswordResetRequestResult(PasswordResetRequestStatus.InvalidEmail, null, TimeSpan.Zero);
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests for one address at once: the other one was taken.
            return new PasswordResetRequestResult(PasswordResetRequestStatus.TooSoon, null, PasswordReset.ResendInterval);
        }

        return new PasswordResetRequestResult(PasswordResetRequestStatus.Requested, entry.Id, PasswordReset.ResendInterval);
    }

    /// <summary>
    /// Spends the code of the request <paramref name="resetId"/>: the account gets <paramref name="password"/>, and
    /// every earlier session of it ends — the provider's sessions, the refresh tokens, the authorizations (ADR-0036).
    /// A password that breaks the rules is refused before the code is checked, so it costs no try.
    /// </summary>
    public async Task<PasswordChangeResult> ResetAsync(Guid resetId, string? code, string? password, CancellationToken cancellationToken)
    {
        if (!PasswordHasher.IsAcceptable(password))
        {
            return new PasswordChangeResult(PasswordChangeStatus.InvalidPassword);
        }

        var entry = await db.PasswordResets.SingleOrDefaultAsync(r => r.Id == resetId, cancellationToken);
        var now = time.GetUtcNow();
        if (entry is null || string.IsNullOrWhiteSpace(code) || !entry.IsUsable(now))
        {
            return new PasswordChangeResult(PasswordChangeStatus.Refused);
        }

        if (!entry.Accepts(code) || entry.AccountId is not { } accountId)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new PasswordChangeResult(PasswordChangeStatus.Refused);
        }

        db.PasswordResets.Remove(entry);
        var account = await db.Accounts.SingleOrDefaultAsync(
            a => a.Id == accountId && a.NormalizedEmail == entry.NormalizedEmail && a.EmailConfirmedAt != null,
            cancellationToken);
        if (account is null)
        {
            // The account gave the address up, or is gone, since the letter.
            await db.SaveChangesAsync(cancellationToken);
            return new PasswordChangeResult(PasswordChangeStatus.Refused);
        }

        // The sessions end first: should saving fail after this, the old password still works and nothing is open.
        var subject = account.Id.ToString("D");
        await tokens.RevokeBySubjectAsync(subject, cancellationToken);
        await authorizations.RevokeBySubjectAsync(subject, cancellationToken);
        await sessions.EndAllAsync(account.Id, cancellationToken);
        account.SetPassword(PasswordHasher.Hash(password!), endSessions: true);
        await SendChangedAsync(account, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new PasswordChangeResult(PasswordChangeStatus.Changed, account.Id);
    }

    /// <summary>
    /// Gives the signed-in account <paramref name="accountId"/> the password <paramref name="newPassword"/>, when
    /// <paramref name="currentPassword"/> is its password. The sessions stay: the owner proved who they are.
    /// </summary>
    public async Task<PasswordChangeResult> ChangeAsync(
        Guid accountId, string? currentPassword, string? newPassword, CancellationToken cancellationToken)
    {
        if (!PasswordHasher.IsAcceptable(newPassword))
        {
            return new PasswordChangeResult(PasswordChangeStatus.InvalidPassword);
        }

        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null
            || string.IsNullOrEmpty(currentPassword)
            || currentPassword.Length > PasswordHasher.MaxLength
            || !PasswordHasher.Verify(currentPassword, account.PasswordHash))
        {
            return new PasswordChangeResult(PasswordChangeStatus.Refused);
        }

        account.SetPassword(PasswordHasher.Hash(newPassword!), endSessions: false);
        await SendChangedAsync(account, time.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new PasswordChangeResult(PasswordChangeStatus.Changed, account.Id);
    }

    // The owner learns of every new password, by a letter to the confirmed address.
    private async Task SendChangedAsync(Account account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (account is not { HasConfirmedEmail: true, Email: { } email })
        {
            return;
        }

        var when = now.UtcDateTime.ToString("d MMMM yyyy 'в' HH:mm '(UTC)'", _culture);
        await mailer.SendAsync(email, ChangedTemplate, _culture, new Dictionary<string, string> { ["when"] = when },
            cancellationToken);
    }
}
