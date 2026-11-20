using Microsoft.EntityFrameworkCore;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.Passwords;

namespace Platform.Modules.Identity.Registration;

/// <summary>What <see cref="Registrations.RegisterAsync"/> and <see cref="Registrations.ResendAsync"/> did.</summary>
public enum RegistrationStatus
{
    /// <summary>A code went to the address.</summary>
    CodeSent,

    /// <summary>Not an address the platform accepts.</summary>
    InvalidEmail,

    /// <summary>The password is shorter than <see cref="PasswordHasher.MinLength"/> or too long.</summary>
    InvalidPassword,

    /// <summary>An account other than a pending registration has the address.</summary>
    Taken,

    /// <summary>The last code to the address was sent less than <see cref="EmailConfirmation.ResendInterval"/> ago.</summary>
    TooSoon,

    /// <summary>No pending registration has this identifier.</summary>
    NotFound,
}

/// <param name="Status">What happened.</param>
/// <param name="RegistrationId">The registration the code belongs to, when a code was sent.</param>
/// <param name="RetryAfter">How long until a new code may be sent.</param>
public sealed record RegistrationResult(RegistrationStatus Status, Guid? RegistrationId, TimeSpan RetryAfter);

/// <summary>
/// Registration by email and password (FR-AUTH, E03-01): the account is created with the address not confirmed, a code
/// goes to the address, and the code confirms it. The identifier of the registration (the new account_id, known only to
/// the browser that registered) is needed with the code, so whoever reads the letter cannot confirm a registration
/// somebody else started with a password of their own.
/// </summary>
/// <remarks>
/// An unconfirmed registration does not lock its address (ADR-0020): registering the same address again replaces it,
/// once its resend timer has run out. Only the newest registration can be confirmed, and only with a code from the
/// mailbox, so a stranger who registered someone's address first gains nothing.
/// </remarks>
public sealed class Registrations(IdentityDbContext db, AccountEmails emails, TimeProvider time)
{
    /// <summary>Creates an account for <paramref name="email"/> with <paramref name="password"/> and sends it a code.</summary>
    public async Task<RegistrationResult> RegisterAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        var normalized = EmailAddresses.Normalize(email);
        if (normalized is null)
        {
            return Failed(RegistrationStatus.InvalidEmail);
        }

        if (!PasswordHasher.IsAcceptable(password))
        {
            return Failed(RegistrationStatus.InvalidPassword);
        }

        var now = time.GetUtcNow();
        var holder = await db.Accounts.SingleOrDefaultAsync(a => a.NormalizedEmail == normalized, cancellationToken);
        if (holder is not null)
        {
            if (!holder.IsPendingRegistration)
            {
                return Failed(RegistrationStatus.Taken);
            }

            var pending = await db.EmailConfirmations.SingleOrDefaultAsync(c => c.AccountId == holder.Id, cancellationToken);
            if (pending?.ResendDelay(now) is { } delay && delay > TimeSpan.Zero)
            {
                return new RegistrationResult(RegistrationStatus.TooSoon, null, delay);
            }

            // The earlier registration was never confirmed: the address goes to the newest one (its code dies with it).
            db.Accounts.Remove(holder);
        }

        var typed = email!.Trim();
        var account = Account.Register(time, typed, normalized, PasswordHasher.Hash(password!));
        db.Accounts.Add(account);
        if (!await emails.QueueCodeAsync(account.Id, null, typed, normalized, cancellationToken))
        {
            return Failed(RegistrationStatus.InvalidEmail);
        }

        return await emails.SaveAsync(cancellationToken)
            ? new RegistrationResult(RegistrationStatus.CodeSent, account.Id, EmailConfirmation.ResendInterval)
            : Failed(RegistrationStatus.Taken);
    }

    /// <summary>Sends a new code for the pending registration <paramref name="registrationId"/>, once its timer allows.</summary>
    public async Task<RegistrationResult> ResendAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        var account = await PendingAsync(registrationId, cancellationToken);
        if (account is null)
        {
            return Failed(RegistrationStatus.NotFound);
        }

        var confirmation = await db.EmailConfirmations.SingleOrDefaultAsync(c => c.AccountId == account.Id, cancellationToken);
        if (confirmation?.ResendDelay(time.GetUtcNow()) is { } delay && delay > TimeSpan.Zero)
        {
            return new RegistrationResult(RegistrationStatus.TooSoon, account.Id, delay);
        }

        if (!await emails.QueueCodeAsync(account.Id, confirmation, account.Email!, account.NormalizedEmail!, cancellationToken))
        {
            return Failed(RegistrationStatus.InvalidEmail);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new RegistrationResult(RegistrationStatus.CodeSent, account.Id, EmailConfirmation.ResendInterval);
    }

    /// <summary>
    /// Confirms the pending registration <paramref name="registrationId"/> with the code from the letter; the account it
    /// confirmed, or <c>null</c> with the reason.
    /// </summary>
    public async Task<(Guid? AccountId, EmailConfirmResult Result)> ConfirmAsync(
        Guid registrationId, string? code, CancellationToken cancellationToken)
    {
        if (await PendingAsync(registrationId, cancellationToken) is null)
        {
            return (null, EmailConfirmResult.Invalid);
        }

        var result = await emails.ConfirmAsync(registrationId, code, cancellationToken);
        return (result == EmailConfirmResult.Confirmed ? registrationId : null, result);
    }

    // Only a registration that is still pending: a confirmed account is never signed in by an identifier and a code.
    private async Task<Account?> PendingAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == registrationId, cancellationToken);
        return account is { IsPendingRegistration: true } ? account : null;
    }

    private static RegistrationResult Failed(RegistrationStatus status) => new(status, null, TimeSpan.Zero);
}
