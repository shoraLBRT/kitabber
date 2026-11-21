using Microsoft.Extensions.Time.Testing;
using Platform.Modules.Identity.Passwords;

namespace Platform.Modules.Identity.IntegrationTests.Passwords;

/// <summary>The rules of a password reset code and of a new password (ADR-0036), without a database.</summary>
public sealed class PasswordResetTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CodeIsAcceptedOnlyWhileItLivesAndWithinItsTries()
    {
        var reset = PasswordReset.Create("reader@example.com");
        reset.Reset(Guid.NewGuid(), "482913", _now);

        Assert.True(reset.IsUsable(_now + PasswordReset.Lifetime - TimeSpan.FromSeconds(1)));
        Assert.False(reset.IsUsable(_now + PasswordReset.Lifetime));
        Assert.True(reset.Accepts(" 482913 "));

        for (var i = 0; i < PasswordReset.MaxAttempts; i++)
        {
            Assert.False(reset.Accepts("000000"));
        }

        Assert.False(reset.IsUsable(_now));
    }

    [Fact]
    public void NewCodeReplacesTheOldOneAndRestartsTheTimer()
    {
        var reset = PasswordReset.Create("reader@example.com");
        reset.Reset(Guid.NewGuid(), "111111", _now);
        var first = reset.Id;
        Assert.Equal(PasswordReset.ResendInterval, reset.ResendDelay(_now));
        Assert.Equal(TimeSpan.Zero, reset.ResendDelay(_now + PasswordReset.ResendInterval));

        reset.Reset(null, "222222", _now + PasswordReset.ResendInterval);

        Assert.NotEqual(first, reset.Id);
        Assert.Null(reset.AccountId);
        Assert.False(reset.Accepts("111111"));
        Assert.True(reset.Accepts("222222"));
    }

    [Fact]
    public void OnlyAResetEndsTheSessions()
    {
        var account = Account.Create(new FakeTimeProvider(_now));
        Assert.Null(account.SessionStamp);

        account.SetPassword(PasswordHasher.Hash("changed-1"), endSessions: false);
        Assert.Null(account.SessionStamp);

        account.SetPassword(PasswordHasher.Hash("reset-1"), endSessions: true);
        var stamp = account.SessionStamp;
        Assert.NotNull(stamp);
        account.SetPassword(PasswordHasher.Hash("reset-2"), endSessions: true);
        Assert.NotEqual(stamp, account.SessionStamp);
        Assert.True(PasswordHasher.Verify("reset-2", account.PasswordHash));
    }
}
