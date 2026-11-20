using Platform.Modules.Identity.Passwords;

namespace Platform.Modules.Identity.IntegrationTests.Registration;

/// <summary>The password rules (FR-AUTH, ADR-0021): at least 6 characters, stored only as an Argon2id hash.</summary>
public sealed class PasswordHasherTests
{
    [Theory]
    [InlineData("123456", true)]
    [InlineData("пароль", true)]
    [InlineData("12345", false)]
    [InlineData("      ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PasswordNeedsSixCharacters(string? password, bool acceptable) =>
        Assert.Equal(acceptable, PasswordHasher.IsAcceptable(password));

    [Fact]
    public void PasswordLongerThanTheLimitIsRefused()
    {
        Assert.True(PasswordHasher.IsAcceptable(new string('a', PasswordHasher.MaxLength)));
        Assert.False(PasswordHasher.IsAcceptable(new string('a', PasswordHasher.MaxLength + 1)));
    }

    [Fact]
    public void HashIsArgon2idWithItsOwnSaltAndVerifiesOnlyThePassword()
    {
        var first = PasswordHasher.Hash("correct horse");
        var second = PasswordHasher.Hash("correct horse");

        Assert.Matches(@"^\$argon2id\$v=19\$m=19456,t=2,p=1\$[A-Za-z0-9+/]{22}\$[A-Za-z0-9+/]{43}$", first);
        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("correct horse", first));
        Assert.True(PasswordHasher.Verify("correct horse", second));
        Assert.False(PasswordHasher.Verify("correct horse ", first));
        Assert.False(PasswordHasher.Verify("Correct horse", first));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$bad")]
    [InlineData("$argon2id$v=19$m=99999999,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$!!!$aGFzaA")]
    public void MalformedHashMatchesNothing(string? stored) =>
        Assert.False(PasswordHasher.Verify("password", stored));
}
