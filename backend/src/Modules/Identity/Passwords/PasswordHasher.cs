using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Platform.Modules.Identity.Passwords;

/// <summary>
/// The rules of an account's password (FR-AUTH, ADR-0021): at least <see cref="MinLength"/> characters, stored only as
/// an Argon2id hash in the PHC string format (<c>$argon2id$v=19$m=…,t=…,p=…$salt$hash</c>), so the parameters can be
/// raised later without breaking the hashes already stored.
/// </summary>
public static class PasswordHasher
{
    public const int MinLength = 6;

    /// <summary>Longer passwords are refused: hashing megabytes of text is a cheap way to load the server.</summary>
    public const int MaxLength = 128;

    // OWASP's minimum for Argon2id: 19 MiB of memory, 2 passes, 1 lane.
    private const int _memoryKib = 19 * 1024;
    private const int _iterations = 2;
    private const int _parallelism = 1;
    private const int _saltLength = 16;
    private const int _hashLength = 32;
    private const string _prefix = "$argon2id$v=19$";

    /// <summary>Whether <paramref name="password"/> is long enough and not too long.</summary>
    public static bool IsAcceptable(string? password) =>
        password is { Length: >= MinLength and <= MaxLength } && !string.IsNullOrWhiteSpace(password);

    /// <summary>An Argon2id hash of <paramref name="password"/> with a new random salt, as a PHC string.</summary>
    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(_saltLength);
        var hash = Compute(password, salt, _memoryKib, _iterations, _parallelism, _hashLength);
        return string.Create(CultureInfo.InvariantCulture,
            $"{_prefix}m={_memoryKib},t={_iterations},p={_parallelism}${Encode(salt)}${Encode(hash)}");
    }

    /// <summary>Whether <paramref name="password"/> matches <paramref name="stored"/>; a malformed hash matches nothing.</summary>
    public static bool Verify(string password, string? stored)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (stored is null || !stored.StartsWith(_prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = stored[_prefix.Length..].Split('$');
        if (parts.Length != 3 || Parameters(parts[0]) is not var (memory, iterations, parallelism)
            || Decode(parts[1]) is not { Length: > 0 } salt || Decode(parts[2]) is not { Length: > 0 } expected)
        {
            return false;
        }

        var actual = Compute(password, salt, memory, iterations, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Compute(string password, byte[] salt, int memory, int iterations, int parallelism, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memory,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon.GetBytes(length);
    }

    private static (int Memory, int Iterations, int Parallelism)? Parameters(string text)
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in text.Split(','))
        {
            var kv = pair.Split('=');
            if (kv.Length != 2 || !int.TryParse(kv[1], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value <= 0)
            {
                return null;
            }

            values[kv[0]] = value;
        }

        // Bounds keep a forged hash from making the server allocate gigabytes.
        return values.TryGetValue("m", out var m) && values.TryGetValue("t", out var t) && values.TryGetValue("p", out var p)
               && m <= 1024 * 1024 && t <= 16 && p <= 16
            ? (m, t, p)
            : null;
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[]? Decode(string text)
    {
        var padded = text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '=');
        try
        {
            return Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
