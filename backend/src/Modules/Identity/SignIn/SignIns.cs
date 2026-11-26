using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Mail;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.Passwords;

namespace Platform.Modules.Identity.SignIn;

/// <summary>What <see cref="SignIns.RequestCodeAsync"/> did.</summary>
public enum SignInCodeStatus
{
    /// <summary>A code went to the address, registered or not.</summary>
    CodeSent,

    /// <summary>Not an address the platform accepts.</summary>
    InvalidEmail,

    /// <summary>The last code to the address was sent less than <see cref="SignInCode.ResendInterval"/> ago.</summary>
    TooSoon,
}

/// <param name="Status">What happened.</param>
/// <param name="ChallengeId">The request the code belongs to, when a code was sent.</param>
/// <param name="RetryAfter">How long until a new code may be sent.</param>
public sealed record SignInCodeResult(SignInCodeStatus Status, Guid? ChallengeId, TimeSpan RetryAfter);

/// <summary>
/// Signing in by email (FR-AUTH, E03-02, ADR-0022): with the password, or with a one-time code from a letter. Neither
/// tells whether an address is registered: a wrong password and an unknown address cost one Argon2 verification each
/// and are refused alike, and a code goes to every address asked for. The code proves the mailbox, so for an address no
/// account has confirmed it creates the account; an account that only typed the address loses it.
/// </summary>
public sealed class SignIns(IdentityDbContext db, AccountEmails emails, IMailer<IdentityDbContext> mailer, TimeProvider time)
{
    public const string Template = "login-code";

    private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru");

    // Verified when no account can sign in with the address, so that answer takes as long as a wrong password.
    private static readonly Lazy<string> _decoy = new(() => PasswordHasher.Hash(Guid.NewGuid().ToString("N")));

    /// <summary>
    /// The account whose confirmed email is <paramref name="email"/> and whose password is <paramref name="password"/>;
    /// <c>null</c> for every other case alike: no such address, no password, a registration not confirmed yet, a wrong
    /// password.
    /// </summary>
    public async Task<Guid?> SignInWithPasswordAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        var normalized = EmailAddresses.Normalize(email);
        var account = normalized is null
            ? null
            : await db.Accounts.AsNoTracking()
                .Where(a => a.NormalizedEmail == normalized && a.EmailConfirmedAt != null && a.PasswordHash != null)
                .Select(a => new { a.Id, a.PasswordHash })
                .SingleOrDefaultAsync(cancellationToken);

        var acceptable = PasswordHasher.IsAcceptable(password);
        var matches = PasswordHasher.Verify(acceptable ? password! : "-", account?.PasswordHash ?? _decoy.Value);
        return matches && acceptable && account is not null ? account.Id : null;
    }

    /// <summary>Sends a one-time code to <paramref name="email"/>, once the resend timer of the last one allows.</summary>
    public async Task<SignInCodeResult> RequestCodeAsync(string? email, CancellationToken cancellationToken)
    {
        var normalized = EmailAddresses.Normalize(email);
        if (normalized is null)
        {
            return new SignInCodeResult(SignInCodeStatus.InvalidEmail, null, TimeSpan.Zero);
        }

        var now = time.GetUtcNow();
        // Expired codes are of no use to anyone; without this the codes of unknown addresses would pile up.
        await db.SignInCodes.Where(c => c.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        var entry = await db.SignInCodes.SingleOrDefaultAsync(c => c.NormalizedEmail == normalized, cancellationToken);
        if (entry?.ResendDelay(now) is { } delay && delay > TimeSpan.Zero)
        {
            return new SignInCodeResult(SignInCodeStatus.TooSoon, null, delay);
        }

        if (entry is null)
        {
            entry = SignInCode.Create(normalized);
            db.SignInCodes.Add(entry);
        }

        var typed = email!.Trim();
        var code = EmailConfirmation.NewCode();
        entry.Reset(typed, code, now);
        try
        {
            // Through the module's outbox: the letter leaves only if the code is saved.
            await mailer.SendAsync(typed, Template, _culture, new Dictionary<string, string>
            {
                ["code"] = code,
                ["lifetime"] = "10 минут",
            }, cancellationToken);
        }
        catch (ArgumentException)
        {
            // An address the normaliser accepts but the mail library does not parse.
            return new SignInCodeResult(SignInCodeStatus.InvalidEmail, null, TimeSpan.Zero);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests for one address at once: the other one sent the code.
            return new SignInCodeResult(SignInCodeStatus.TooSoon, null, SignInCode.ResendInterval);
        }

        return new SignInCodeResult(SignInCodeStatus.CodeSent, entry.Id, SignInCode.ResendInterval);
    }

    /// <summary>
    /// Spends the code of the request <paramref name="challengeId"/>: the account with that confirmed address, or a new
    /// one with it; <c>null</c> when the code is wrong, expired or spent.
    /// </summary>
    public async Task<Guid?> SignInWithCodeAsync(Guid challengeId, string? code, CancellationToken cancellationToken)
    {
        var entry = await db.SignInCodes.SingleOrDefaultAsync(c => c.Id == challengeId, cancellationToken);
        var now = time.GetUtcNow();
        if (entry is null || string.IsNullOrWhiteSpace(code) || !entry.IsUsable(now))
        {
            return null;
        }

        if (!entry.Accepts(code))
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        db.SignInCodes.Remove(entry);
        var account = await db.Accounts.SingleOrDefaultAsync(
            a => a.NormalizedEmail == entry.NormalizedEmail && a.EmailConfirmedAt != null, cancellationToken);
        if (account is not null)
        {
            await db.SaveChangesAsync(cancellationToken);
            return account.Id;
        }

        // Nobody has confirmed the address: the code has, so it gets an account of its own (without a password).
        await emails.ReleaseUnconfirmedAsync(entry.NormalizedEmail, null, cancellationToken);
        account = Account.Create(time);
        account.ConfirmEmail(entry.Email, entry.NormalizedEmail, now);
        db.Accounts.Add(account);
        return await emails.SaveAsync(cancellationToken) ? account.Id : null;
    }
}
