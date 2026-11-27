namespace Platform.Modules.Preferences.IntegrationTests;

/// <summary>E04-08: the rules of the settings and their model. No database.</summary>
public sealed class PreferenceRulesTests
{
    [Theory]
    [InlineData("system", ThemeChoice.System)]
    [InlineData("light", ThemeChoice.Light)]
    [InlineData(" Dark ", ThemeChoice.Dark)]
    public void KnownThemeIsTaken(string name, ThemeChoice theme) =>
        Assert.Equal(theme, PreferenceRules.ParseTheme(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sepia")]
    public void UnknownThemeIsRefused(string? name) => Assert.Null(PreferenceRules.ParseTheme(name));

    [Fact]
    public void ThemeNamesGoBothWays()
    {
        foreach (var theme in Enum.GetValues<ThemeChoice>())
        {
            Assert.Equal(theme, PreferenceRules.ParseTheme(PreferenceRules.NameOf(theme)));
        }
    }

    [Fact]
    public void NewAccountFollowsTheSystemTheme()
    {
        var account = Guid.CreateVersion7();

        var preferences = AccountPreferences.For(account);

        Assert.Equal(account, preferences.AccountId);
        Assert.Equal(ThemeChoice.System, preferences.Theme);
        Assert.Throws<ArgumentException>(() => AccountPreferences.For(Guid.Empty));
    }

    [Theory]
    [InlineData("popular", HomeFeed.Popular)]
    [InlineData(" Fresh ", HomeFeed.Fresh)]
    [InlineData("my", HomeFeed.My)]
    public void KnownHomeFeedIsTaken(string name, HomeFeed feed) =>
        Assert.Equal(feed, PreferenceRules.ParseHomeFeed(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("best")]
    public void UnknownHomeFeedIsRefused(string? name) => Assert.Null(PreferenceRules.ParseHomeFeed(name));

    [Fact]
    public void HomeFeedNamesGoBothWays()
    {
        foreach (var feed in Enum.GetValues<HomeFeed>())
        {
            Assert.Equal(feed, PreferenceRules.ParseHomeFeed(PreferenceRules.NameOf(feed)));
        }
    }

    [Fact]
    public void NewAccountOpensPopularAndAutoplaysVideo()
    {
        var preferences = AccountPreferences.For(Guid.CreateVersion7());

        Assert.Equal(HomeFeed.Popular, preferences.HomeFeed);
        Assert.True(preferences.AutoplayVideo);
    }
}
