using Platform.Testing.Time;

namespace Platform.Modules.Identity.IntegrationTests;

/// <summary>The account_id rules (FR-ID-1): a UUID v7 taken at the time the account is created. No database.</summary>
public sealed class AccountTests
{
    [Fact]
    public void NewAccountGetsUuidV7AtTheCurrentTime()
    {
        var clock = TestClock.Create();

        var account = Account.Create(clock);

        Assert.True(AccountIds.IsVersion7(account.Id));
        Assert.Equal(TestClock.Start, account.CreatedAt);
        // The first 48 bits of a UUID v7 are the creation time in Unix milliseconds.
        var millis = Convert.ToInt64(account.Id.ToString("N")[..12], 16);
        Assert.Equal(TestClock.Start.ToUnixTimeMilliseconds(), millis);
    }

    [Fact]
    public void LaterAccountsSortAfterEarlierOnes()
    {
        var clock = TestClock.Create();
        var first = Account.Create(clock);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        var second = Account.Create(clock);

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(string.CompareOrdinal(first.Id.ToString(), second.Id.ToString()) < 0);
    }

    [Fact]
    public void OnlyVersion7IdentifiersAreAccountIds()
    {
        Assert.False(AccountIds.IsVersion7(Guid.Empty));
        Assert.False(AccountIds.IsVersion7(Guid.NewGuid())); // version 4
        Assert.True(AccountIds.IsVersion7(AccountIds.New(TestClock.Start)));
    }
}
