using System.Security.Cryptography;
using System.Text;
using Platform.Modules.Identity.Email;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// A one-time sign-in code sent to an address (FR-AUTH, ADR-0022): at most one per normalised address, a new request
/// replaces it. It is stored for every address asked for, whether an account has it or not, so the answers and the
/// resend timer do not tell which addresses are registered. Only a hash of the code is kept, bound to the request's
/// <see cref="Id"/>, which only the browser that asked knows: a stranger cannot spend the owner's tries.
/// </summary>
public sealed class SignInCode
{
    /// <summary>How long a code is valid: short, since it signs in.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Wrong codes accepted before the code is spent; the owner then requests a new one.</summary>
    public const int MaxAttempts = 5;

    /// <summary>A new code to the same address no sooner than this after the last one (the resend timer, FR-AUTH).</summary>
    public static TimeSpan ResendInterval => EmailConfirmation.ResendInterval;

    /// <summary>The address the code went to, normalised (<see cref="EmailAddresses.Normalize"/>): the key.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    /// <summary>The request the code belongs to: a random identifier, new with every code.</summary>
    public Guid Id { get; private set; }

    /// <summary>The address as typed; a new account gets it as its email.</summary>
    public string Email { get; private set; } = string.Empty;

    public byte[] CodeHash { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public static SignInCode Create(string normalizedEmail) => new() { NormalizedEmail = normalizedEmail };

    /// <summary>Starts over with <paramref name="code"/> sent to <paramref name="email"/> at <paramref name="now"/>.</summary>
    public void Reset(string email, string code, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Email = email;
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
