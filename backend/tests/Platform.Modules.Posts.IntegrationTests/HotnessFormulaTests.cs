using Microsoft.Extensions.Configuration;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E08-05: the hotness formula on reference examples (ADR-0086) and its coefficients read from the settings, without
/// Docker.
/// </summary>
public sealed class HotnessFormulaTests
{
    private static readonly HotnessCoefficients _defaults = HotnessCoefficients.Default;
    private static readonly DateTimeOffset _epoch = HotnessFormula.Epoch;

    // Reference examples with the default coefficients: a like 1, a comment 2, an opening 0.02, ten times per 12.5 h.
    [Theory]
    [InlineData(0, 0, 0, 0, 0.0)] // nothing, at the epoch
    [InlineData(0, 0, 0, 12.5, 1.0)] // nothing, one decay later
    [InlineData(10, 0, 0, 0, 1.0)] // ten likes at the epoch weigh as much as one decay of youth
    [InlineData(5, 2, 250, 0, 1.146128)] // 5 + 2 · 2 + 250 · 0.02 = 14 points: log10(14)
    [InlineData(100, 0, 0, 25, 4.0)] // a hundred likes, two decays later
    [InlineData(0, 0, 25, 0, 0.0)] // half a point counts as none: under one point the order is zero
    [InlineData(-100, 0, 0, 0, -2.0)] // a negative rating pulls down by its order
    [InlineData(3, 1, 0, 37.5, 3.69897)] // 5 points, three decays later: 3 + log10(5)
    public void ScoreOfReferencePosts(int rating, int comments, int views, double hoursAfterEpoch, double expected)
    {
        var score = HotnessFormula.Score(
            new HotnessInputs(rating, comments, views), _epoch.AddHours(hoursAfterEpoch), _defaults);

        Assert.Equal(expected, score, 6);
    }

    [Fact]
    public void YoungerPostNeedsTenTimesFewerPointsPerDecay()
    {
        var older = HotnessFormula.Score(new HotnessInputs(1000, 0, 0), _epoch.AddDays(30), _defaults);
        var younger = HotnessFormula.Score(new HotnessInputs(100, 0, 0), _epoch.AddDays(30).AddHours(12.5), _defaults);
        var youngerByLess = HotnessFormula.Score(new HotnessInputs(100, 0, 0), _epoch.AddDays(30).AddHours(12), _defaults);

        Assert.Equal(older, younger, 6);
        Assert.True(youngerByLess < older);
    }

    [Fact]
    public void ScoreIsRoundedToSevenDecimals()
    {
        var score = HotnessFormula.Score(new HotnessInputs(7, 0, 0), _epoch.AddSeconds(1), _defaults);

        Assert.Equal(Math.Round(score, 7), score);
    }

    [Fact]
    public void WeightsComeFromTheCoefficients()
    {
        var comments = _defaults with { RatingWeight = 0, CommentWeight = 10, ViewWeight = 0 };

        Assert.Equal(2.0, HotnessFormula.Score(new HotnessInputs(50, 10, 1000), _epoch, comments), 6);
        Assert.Equal(14.0, HotnessFormula.Points(new HotnessInputs(5, 2, 250), _defaults), 6);
    }

    [Fact]
    public void FormulaNamesTheCoefficientsOfTheScoreButNotTheWindow()
    {
        Assert.Equal("r=1;c=2;v=0.02;d=12.5", _defaults.Formula);
        Assert.Equal(_defaults.Formula, (_defaults with { WindowDays = 30 }).Formula);
        Assert.NotEqual(_defaults.Formula, (_defaults with { ViewWeight = 0.03 }).Formula);
    }

    [Fact]
    public void SettingsGiveTheDefaultsWhenEmpty()
    {
        Assert.Equal(_defaults, PostsSettings.ReadHotness(Configuration()));
        Assert.Equal(TimeSpan.FromMinutes(1), PostsSettings.ReadHotnessInterval(Configuration()));
    }

    [Fact]
    public void SettingsGiveTheConfiguredCoefficients()
    {
        var coefficients = PostsSettings.ReadHotness(Configuration(
            (PostsSettings.HotnessRatingWeightKey, "1.5"),
            (PostsSettings.HotnessCommentWeightKey, "0"),
            (PostsSettings.HotnessViewWeightKey, "0.1"),
            (PostsSettings.HotnessDecayHoursKey, "24"),
            (PostsSettings.HotnessWindowDaysKey, "30")));

        Assert.Equal(new HotnessCoefficients(1.5, 0, 0.1, 24, 30), coefficients);
        Assert.Equal(TimeSpan.Zero, PostsSettings.ReadHotnessInterval(
            Configuration((PostsSettings.HotnessIntervalSecondsKey, "0"))));
    }

    [Theory]
    [InlineData(PostsSettings.HotnessRatingWeightKey, "-1")]
    [InlineData(PostsSettings.HotnessCommentWeightKey, "1001")]
    [InlineData(PostsSettings.HotnessViewWeightKey, "много")]
    [InlineData(PostsSettings.HotnessDecayHoursKey, "0")]
    [InlineData(PostsSettings.HotnessDecayHoursKey, "8761")]
    [InlineData(PostsSettings.HotnessWindowDaysKey, "0")]
    [InlineData(PostsSettings.HotnessWindowDaysKey, "1.5")]
    [InlineData(PostsSettings.HotnessIntervalSecondsKey, "3601")]
    public void InvalidSettingIsRefused(string key, string value)
    {
        var configuration = Configuration((key, value));

        var error = Assert.Throws<InvalidOperationException>(() =>
        {
            PostsSettings.ReadHotness(configuration);
            PostsSettings.ReadHotnessInterval(configuration);
        });
        Assert.StartsWith(key, error.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();
}
