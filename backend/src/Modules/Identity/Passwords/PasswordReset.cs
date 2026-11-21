using System.Security.Cryptography;
using System.Text;
using Platform.Modules.Identity.Email;

namespace Platform.Modules.Identity.Passwords;

/// <summary>
/// A request to reset the password of the account with an address (FR-AUTH, E03-05, ADR-0036): at most one per
/// normalised address, a new request replaces it. It is stored for every address asked for, so the answers and the
/// resend timer do not tell which addresses are registered, but only an account's address gets a letter and only its
/// code can be spent. Only a hash of the code is kept, bound to <see cref="Id"/>, which only the browser that asked
/// knows, so a stranger cannot spend the owner's tries.
/// </summary>
public sealed class PasswordReset
{
    /// <summary>How long a code is valid: it gives the account a new password, so not long.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>The lifetime as the letter says it.</summary>
    public const string LifetimeText = "15 минут";

    /// <summary>Wrong codes accepted before the code is spent; the owner then requests a new one.</summary>
    public const int MaxAttempts = 5;

    /// <summary>A new code to the same address no sooner than this after the last one (the resend timer, FR-AUTH).</summary>
    public static TimeSpan ResendInterval => EmailConfirmation.ResendInterval;

    /// <summary>The address the request names, normalised (<see cref="EmailAddresses.Normalize"/>): the key.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    /// <summary>The request: a random identifier, new with every code.</summary>
    public Guid Id { get; private set; }

    /// <summary>The account with that confirmed address; <c>null</c> when there is none and no letter went out.</summary>
    public Guid? AccountId { get; private set; }

    public byte[] CodeHash { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public static PasswordReset Create(string normalizedEmail) => new() { NormalizedEmail = normalizedEmail };

    /// <summary>Starts over at <paramref name="now"/> with <paramref name="code"/> for <paramref name="accountId"/>.</summary>
    public void Reset(Guid? accountId, string code, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        AccountId = accountId;
        CodeHash = Hash(Id, code);
        CreatedAt = now;
        ExpiresAt = now + Lifetime;
        FailedAttempts = 0;
    }

    /// <summary>How long after <paramref name="now"/> a new code may be sent; zero when it may be sent now.</summary>
    public TimeSpan ResendDelay(DateTimeOffset now) =>
        CreatedAt + ResendInterval > now ? CreatedAt + ResendInterval - now : TimeSpan.Zero;

    /// <summary>Whether the code can still be used at <paramref name="now"/>.</summary>
    public bool IsUsable(DateTimeOffset now) => now < ExpiresAt && FailedAttempts < MaxAttempts;

    /// <summary>Checks <paramref name="code"/>; a wrong one counts as a failed attempt.</summary>
    public bool Accepts(string code)
    {
        if (CryptographicOperations.FixedTimeEquals(Hash(Id, code.Trim()), CodeHash))
        {
            return true;
        }

        FailedAttempts++;
        return false;
    }

    private static byte[] Hash(Guid id, string code) => SHA256.HashData(Encoding.UTF8.GetBytes($"{id:D}:{code}"));
}
