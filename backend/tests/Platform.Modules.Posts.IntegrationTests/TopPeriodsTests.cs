namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>E08-06: the periods of the top and how far back each reaches (ADR-0104), without Docker.</summary>
public sealed class TopPeriodsTests
{
    [Theory]
    [InlineData("day", 1)]
    [InlineData("week", 7)]
    [InlineData("month", 30)]
    [InlineData("year", 365)]
    public void PeriodReachesBackItsDays(string period, int days)
    {
        Assert.True(TopPeriods.IsPeriod(period));
        Assert.Equal(TimeSpan.FromDays(days), TopPeriods.Length(period));
    }

    [Fact]
    public void AllTimeHasNoBound()
    {
        Assert.True(TopPeriods.IsPeriod("all"));
        Assert.Null(TopPeriods.Length("all"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Day")]
    [InlineData("decade")]
    public void OtherNamesAreNotPeriods(string? name)
    {
        Assert.False(TopPeriods.IsPeriod(name));
    }

    [Fact]
    public void PeriodsAreInTheOrderOfTheSwitch()
    {
        Assert.Equal(["day", "week", "month", "year", "all"], TopPeriods.Names);
    }
}
