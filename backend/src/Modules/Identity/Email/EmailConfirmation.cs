using System.Security.Cryptography;
using System.Text;

namespace Platform.Modules.Identity.Email;

/// <summary>
/// The code that confirms an email (ADR-0020): at most one per account, a new request replaces it. Only a hash of the
/// code is stored, bound to the account, so a code read from the database or sent to another account confirms nothing.
/// </summary>
public sealed class EmailConfirmation
{
    /// <summary>How long a code is valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    /// <summary>Wrong codes accepted before the code is spent; the owner then requests a new one.</summary>
    public const int MaxAttempts = 5;

    public const int CodeLength = 6;

    /// <summary>A registration gets a new code no sooner than this after the last one (the resend timer, FR-AUTH).</summary>
    public static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(60);

    public Guid AccountId { get; private set; }

    /// <summary>The address being confirmed, as typed.</summary>
    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public byte[] CodeHash { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int FailedAttempts { get; private set; }

    /// <summary>A random code of <see cref="CodeLength"/> digits.</summary>
    public static string NewCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    public static EmailConfirmation Create(Guid accountId) => new() { AccountId = accountId };

    /// <summary>Starts over with <paramref name="code"/> for <paramref name="email"/>, valid from <paramref name="now"/>.</summary>
    public void Reset(string email, string normalizedEmail, string code, DateTimeOffset now)
    {
        Email = email;
        NormalizedEmail = normalizedEmail;
        CodeHash = Hash(AccountId, code);
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
        if (CryptographicOperations.FixedTimeEquals(Hash(AccountId, code.Trim()), CodeHash))
        {
            return true;
        }

        FailedAttempts++;
        return false;
    }

    private static byte[] Hash(Guid accountId, string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{accountId:D}:{code}"));
}
