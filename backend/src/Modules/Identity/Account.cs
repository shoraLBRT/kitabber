namespace Platform.Modules.Identity;

/// <summary>
/// An account (FR-ID-1): the global identity of a person, the part that a future shared account service takes over.
/// The profile (name, nickname, avatar) is not here: it belongs to the Blogs module and refers to the account only by
/// <see cref="Id"/>. External sign-ins, sessions and consents join the account with their own tasks (E18).
/// </summary>
public sealed class Account
{
    /// <summary>The account_id: a UUID v7 for every account the platform creates (ADR-0002).</summary>
    public Guid Id { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>The email as the person typed it (trimmed); letters go to this address.</summary>
    public string? Email { get; private set; }

    /// <summary>
    /// <see cref="Email"/> normalised by <see cref="Email.EmailAddresses.Normalize"/>: no two accounts share it
    /// (ADR-0020). Confirmed, it is the key a future merge of accounts matches people by.
    /// </summary>
    public string? NormalizedEmail { get; private set; }

    /// <summary>When the owner proved the email is theirs; <c>null</c> while it is not confirmed.</summary>
    public DateTimeOffset? EmailConfirmedAt { get; private set; }

    /// <summary>Whether the account may publish and comment: its email is confirmed.</summary>
    public bool HasConfirmedEmail => EmailConfirmedAt is not null;

    /// <summary>
    /// The Argon2id hash of the password (<see cref="Passwords.PasswordHasher"/>, ADR-0021); <c>null</c> for an account
    /// that signs in only in other ways.
    /// </summary>
    public string? PasswordHash { get; private set; }

    /// <summary>
    /// A registration by email and password that is not confirmed yet (ADR-0020): it holds its address only until the
    /// same address is registered again, and only its confirmation signs the person in.
    /// </summary>
    public bool IsPendingRegistration => PasswordHash is not null && EmailConfirmedAt is null;

    /// <summary>A new account registered with <paramref name="email"/> (not confirmed) and a password hash.</summary>
    public static Account Register(TimeProvider time, string email, string normalizedEmail, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        var account = Create(time);
        account.SetUnconfirmedEmail(email, normalizedEmail);
        account.PasswordHash = passwordHash;
        return account;
    }

    /// <summary>A new account with a UUID v7 identifier taken at the current time of <paramref name="time"/>.</summary>
    public static Account Create(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var now = time.GetUtcNow();
        return new Account { Id = AccountIds.New(now), CreatedAt = now };
    }

    /// <summary>Sets an email that is not confirmed yet; a confirmed one is replaced only by <see cref="ConfirmEmail"/>.</summary>
    public void SetUnconfirmedEmail(string email, string normalizedEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        if (HasConfirmedEmail)
        {
            throw new InvalidOperationException("A confirmed email is replaced only by another confirmed one.");
        }

        Email = email;
        NormalizedEmail = normalizedEmail;
    }

    /// <summary>Makes <paramref name="email"/> the account's email, confirmed at <paramref name="confirmedAt"/>.</summary>
    public void ConfirmEmail(string email, string normalizedEmail, DateTimeOffset confirmedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        Email = email;
        NormalizedEmail = normalizedEmail;
        EmailConfirmedAt = confirmedAt;
    }
}

/// <summary>The rules of account_id (FR-ID-1): a UUID v7, ordered by the time the account was created.</summary>
public static class AccountIds
{
    /// <summary>A UUID v7 whose timestamp is <paramref name="createdAt"/>.</summary>
    public static Guid New(DateTimeOffset createdAt) => Guid.CreateVersion7(createdAt);

    /// <summary>Whether <paramref name="id"/> is a UUID v7 of the RFC 9562 variant.</summary>
    public static bool IsVersion7(Guid id) => id.Version == 7 && id.Variant is >= 0x8 and <= 0xB;
}
