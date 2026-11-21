using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Platform.Configuration;
using Platform.Modules.Identity.SignIn;

namespace Platform.Modules.Identity.Captcha;

/// <summary>
/// What the browser needs to pass the captcha (E03-12, ADR-0038). <paramref name="Provider"/> names the widget:
/// <c>proof-of-work</c> fills the other fields, <c>fake</c> (tests only) fills none.
/// </summary>
/// <param name="Provider">The captcha provider: <c>proof-of-work</c>, or <c>fake</c> in tests.</param>
/// <param name="Algorithm">The hash of the proof of work: <c>SHA-256</c>.</param>
/// <param name="Challenge">The hash to find: hex of SHA-256 of the salt followed by the secret number.</param>
/// <param name="Salt">The salt the number follows, with its expiry (<c>?expires=</c> in Unix seconds).</param>
/// <param name="Signature">The server's signature of the challenge; sent back with the solution.</param>
/// <param name="MaxNumber">The largest number to try, from zero.</param>
public sealed record CaptchaChallenge(
    string Provider, string? Algorithm, string? Challenge, string? Salt, string? Signature, long? MaxNumber);

/// <summary>
/// The captcha provider (E03-12, ADR-0038), like every external service behind an interface with a fake. The sign-in
/// asks for a solution only after a series of failed attempts; the provider issues a challenge and checks a solution.
/// </summary>
public interface ICaptcha
{
    /// <summary>A new challenge for the browser.</summary>
    CaptchaChallenge CreateChallenge();

    /// <summary>Whether <paramref name="solution"/> solves a challenge of this provider; each solution passes once.</summary>
    Task<bool> VerifyAsync(string? solution, CancellationToken cancellationToken);
}

/// <summary>The providers of <see cref="IdentitySettings.CaptchaProviderKey"/>.</summary>
public enum CaptchaProvider
{
    /// <summary>The platform's own proof of work (ADR-0038): no third party, no keys of its own.</summary>
    ProofOfWork,

    /// <summary>The fake for tests: accepts <see cref="FakeCaptcha.Solution"/>. Refused outside Development and Test.</summary>
    Fake,
}

/// <summary>The fake captcha for tests and local runs: any challenge is solved by <see cref="Solution"/>.</summary>
public sealed class FakeCaptcha : ICaptcha
{
    public const string Name = "fake";

    /// <summary>The one solution the fake accepts.</summary>
    public const string Solution = "fake-captcha-solved";

    public CaptchaChallenge CreateChallenge() => new(Name, null, null, null, null, null);

    public Task<bool> VerifyAsync(string? solution, CancellationToken cancellationToken) =>
        Task.FromResult(string.Equals(solution, Solution, StringComparison.Ordinal));
}

/// <summary>The key that signs proof-of-work challenges.</summary>
#pragma warning disable CA1819 // A key is bytes.
public sealed record CaptchaKey(byte[] Value);
#pragma warning restore CA1819

internal static class CaptchaServiceCollectionExtensions
{
    /// <summary>
    /// The provider of <see cref="IdentitySettings.CaptchaProviderKey"/>. The proof of work signs its challenges with a
    /// key derived from the provider's encryption key, so every instance of a stand accepts the others' challenges;
    /// where that key is generated at start (Development, Test) a random key is enough.
    /// </summary>
    public static IServiceCollection AddCaptcha(
        this IServiceCollection services, IConfiguration configuration, string environment, SymmetricSecurityKey? encryptionKey)
    {
        var value = configuration[IdentitySettings.CaptchaProviderKey];
        var provider = CaptchaProvider.ProofOfWork;
        if (!string.IsNullOrWhiteSpace(value)
            && !(Enum.TryParse(value, ignoreCase: true, out provider) && Enum.IsDefined(provider)))
        {
            throw new InvalidConfigurationException(
                [$"{IdentitySettings.CaptchaProviderKey} = '{value}': expected one of {string.Join(", ", Enum.GetNames<CaptchaProvider>())}"]);
        }

        if (provider == CaptchaProvider.Fake)
        {
            if (!ProviderKeys.MayGenerateKeys(environment))
            {
                throw new InvalidConfigurationException(
                    [$"{IdentitySettings.CaptchaProviderKey} = Fake is allowed only in Development and Test, not in {environment}"]);
            }

            services.TryAddSingleton<ICaptcha, FakeCaptcha>();
            return services;
        }

        var key = encryptionKey is null
            ? RandomNumberGenerator.GetBytes(32)
            : HKDF.DeriveKey(HashAlgorithmName.SHA256, encryptionKey.Key, 32, info: Encoding.UTF8.GetBytes("identity:captcha"));
        services.TryAddSingleton(new CaptchaKey(key));
        services.TryAddSingleton<ICaptcha, ProofOfWorkCaptcha>();
        return services;
    }
}
