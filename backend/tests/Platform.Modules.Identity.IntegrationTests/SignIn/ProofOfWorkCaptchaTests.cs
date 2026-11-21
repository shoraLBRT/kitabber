using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Platform.Caching;
using Platform.Modules.Identity.Captcha;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>The platform's own captcha (E03-12, ADR-0038): a proof of work the browser solves. No containers.</summary>
public sealed class ProofOfWorkCaptchaTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly CaptchaKey _key = new(RandomNumberGenerator.GetBytes(32));
    private readonly Counter _counter = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SolvedChallengePassesOnce()
    {
        var captcha = Create();
        var challenge = captcha.CreateChallenge();

        Assert.Equal(ProofOfWorkCaptcha.Name, challenge.Provider);
        Assert.Equal("SHA-256", challenge.Algorithm);
        Assert.Equal(ProofOfWorkCaptcha.MaxNumber, challenge.MaxNumber);
        var solution = Solve(challenge);
        Assert.True(await captcha.VerifyAsync(solution, Ct));
        Assert.False(await captcha.VerifyAsync(solution, Ct));
    }

    [Fact]
    public async Task WrongNumberForgedChallengeAndGarbageFail()
    {
        var captcha = Create();
        var challenge = captcha.CreateChallenge();
        var number = Number(challenge);

        Assert.False(await captcha.VerifyAsync(Encode(challenge, number + 1), Ct));
        Assert.False(await captcha.VerifyAsync(Solve(new CaptchaChallenge(
            challenge.Provider, challenge.Algorithm, challenge.Challenge, challenge.Salt, new string('0', 64), challenge.MaxNumber)), Ct));
        Assert.False(await captcha.VerifyAsync(Solve(Create(new CaptchaKey(RandomNumberGenerator.GetBytes(32))).CreateChallenge()), Ct));
        foreach (var garbage in new[] { null, "", "not base64!", Convert.ToBase64String("{}"u8.ToArray()) })
        {
            Assert.False(await captcha.VerifyAsync(garbage, Ct));
        }

        Assert.True(await captcha.VerifyAsync(Encode(challenge, number), Ct));
    }

    [Fact]
    public async Task ExpiredChallengeFails()
    {
        var captcha = Create();
        var challenge = captcha.CreateChallenge();
        var solution = Solve(challenge);

        _time.Advance(ProofOfWorkCaptcha.Lifetime + TimeSpan.FromSeconds(1));

        Assert.False(await captcha.VerifyAsync(solution, Ct));
    }

    private ProofOfWorkCaptcha Create(CaptchaKey? key = null) =>
        new(key ?? _key, _counter, _time, NullLogger<ProofOfWorkCaptcha>.Instance);

    // The browser's work: try every number until the hash of the salt and the number is the challenge.
    private static long Number(CaptchaChallenge challenge)
    {
        for (var number = 0L; number <= challenge.MaxNumber; number++)
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(challenge.Salt + number.ToString(CultureInfo.InvariantCulture))));
            if (hash == challenge.Challenge)
            {
                return number;
            }
        }

        throw new InvalidOperationException("no solution");
    }

    private static string Solve(CaptchaChallenge challenge) => Encode(challenge, Number(challenge));

    private static string Encode(CaptchaChallenge challenge, long number) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
        {
            algorithm = challenge.Algorithm,
            challenge = challenge.Challenge,
            number,
            salt = challenge.Salt,
            signature = challenge.Signature,
        }));

    private sealed class Counter : IRateCounter
    {
        private readonly Dictionary<string, long> _values = [];

        public Task<long> IncrementAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values[key] = _values.GetValueOrDefault(key) + 1);

        public Task<long> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.GetValueOrDefault(key));
    }
}
