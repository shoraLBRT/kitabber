using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Caching;

namespace Platform.Modules.Identity.Captcha;

/// <summary>
/// The platform's own captcha (ADR-0038): a proof of work in the format of the open ALTCHA protocol. The server picks
/// a secret number up to <see cref="MaxNumber"/>, sends the SHA-256 of a salt followed by it and signs that hash; the
/// browser finds the number by trying them all, which takes a person's browser a second or two and makes every
/// guessed password cost a bot the same. Nothing leaves the platform and nothing is stored about the person: the
/// salt carries the expiry, the signature proves the challenge is ours, and a counter makes each solution pass once.
/// </summary>
public sealed partial class ProofOfWorkCaptcha(
    CaptchaKey key, IRateCounter counter, TimeProvider time, ILogger<ProofOfWorkCaptcha> logger) : ICaptcha
{
    public const string Name = "proof-of-work";

    public const string Algorithm = "SHA-256";

    /// <summary>The largest secret number: on average the browser computes half as many hashes.</summary>
    public const long MaxNumber = 100_000;

    /// <summary>How long a challenge may be solved and sent back.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private const string _expiresMarker = "?expires=";
    private const string _spentPrefix = "identity:captcha:";

    public CaptchaChallenge CreateChallenge()
    {
        var expires = (time.GetUtcNow() + Lifetime).ToUnixTimeSeconds();
        var salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12)) + _expiresMarker
            + expires.ToString(CultureInfo.InvariantCulture);
        var number = RandomNumberGenerator.GetInt32(0, (int)MaxNumber + 1);
        var challenge = Hash(salt, number);
        return new CaptchaChallenge(Name, Algorithm, challenge, salt, Sign(challenge), MaxNumber);
    }

    public async Task<bool> VerifyAsync(string? solution, CancellationToken cancellationToken)
    {
        if (Parse(solution) is not { } payload
            || !string.Equals(payload.Algorithm, Algorithm, StringComparison.Ordinal)
            || payload.Number is not (>= 0 and <= MaxNumber)
            || Expires(payload.Salt) is not { } expires
            || expires <= time.GetUtcNow().ToUnixTimeSeconds()
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(payload.Challenge!)),
                Encoding.ASCII.GetBytes(payload.Signature!))
            || !string.Equals(Hash(payload.Salt!, payload.Number.Value), payload.Challenge, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return await counter.IncrementAsync(_spentPrefix + payload.Signature, Lifetime, cancellationToken) == 1;
        }
#pragma warning disable CA1031 // As with rate limits (ADR-0016), a failing counter store lets a valid solution through.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogCounterFailed(logger, exception);
            return true;
        }
#pragma warning restore CA1031
    }

    private static string Hash(string salt, long number) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(salt + number.ToString(CultureInfo.InvariantCulture))));

    private string Sign(string challenge) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(key.Value, Encoding.UTF8.GetBytes(challenge)));

    private static long? Expires(string? salt)
    {
        var at = salt?.IndexOf(_expiresMarker, StringComparison.Ordinal) ?? -1;
        return at >= 0 && long.TryParse(salt.AsSpan(at + _expiresMarker.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static Payload? Parse(string? solution)
    {
        if (string.IsNullOrWhiteSpace(solution) || solution.Length > 2048)
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(Convert.FromBase64String(solution), JsonSerializerOptions.Web);
            return payload is { Challenge: not null, Salt: not null, Signature: not null } ? payload : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The solution the browser sends: base64 of this JSON, as in ALTCHA.</summary>
    internal sealed record Payload(string? Algorithm, string? Challenge, long? Number, string? Salt, string? Signature);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Captcha counters are unavailable; a valid solution is let through")]
    private static partial void LogCounterFailed(ILogger logger, Exception exception);
}
