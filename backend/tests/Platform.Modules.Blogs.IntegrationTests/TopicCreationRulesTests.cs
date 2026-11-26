using Microsoft.Extensions.Configuration;
using Platform.Modules.Blogs.Topics;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>E11-11, ADR-0084: the stop list, the reserved addresses and the limits of creating topics.</summary>
public sealed class TopicCreationRulesTests
{
    private static readonly IReadOnlyList<string> _stopWords = TopicLimits.Default.StopWords;

    [Theory]
    [InlineData("Официальные новости", "официальн")]
    [InlineData("АДМИНИСТРАЦИЯ сайта", "администр")]
    [InlineData("admin_news", "admin")]
    [InlineData("Служба поддержки", "поддержк")]
    [InlineData("Official-Club", "official")]
    public void StopListCatchesAWordThatStartsWithAnEntry(string text, string entry) =>
        Assert.Equal(entry, TopicCreationRules.StopWordIn(text, _stopWords));

    [Theory]
    [InlineData("Бадминтон")]
    [InlineData("badminton")]
    [InlineData("Наука")]
    [InlineData("")]
    [InlineData(null)]
    public void StopListLeavesOtherWordsAlone(string? text) => Assert.Null(TopicCreationRules.StopWordIn(text, _stopWords));

    [Fact]
    public void StopListComparesWithoutCaseAndWithЁAsЕ()
    {
        var stopWords = TopicCreationRules.ParseStopWords(" Ёлка , ,ЁЖ");

        Assert.Equal(["елка", "еж"], stopWords);
        Assert.Equal("еж", TopicCreationRules.StopWordIn("Ежики в тумане", stopWords));
    }

    [Theory]
    [InlineData("settings")]
    [InlineData("write")]
    [InlineData("topics")]
    [InlineData("api")]
    [InlineData("admin")]
    public void ReservedAddressesAreRefused(string nickname) => Assert.True(TopicCreationRules.IsReservedAddress(nickname));

    [Fact]
    public void ScienceIsNotReserved() => Assert.False(TopicCreationRules.IsReservedAddress("science"));

    // A topic lives at /<address>: a page of the site there would hide it. Every top-level page of the site is reserved.
    [Fact]
    public void EveryTopLevelPageOfTheSiteIsAReservedAddress()
    {
        var pages = Path.Combine(FindRoot(), "frontend", "pages");
        var names = Directory.EnumerateFileSystemEntries(pages)
            .Select(path => Path.GetFileNameWithoutExtension(path).ToLowerInvariant())
            .Where(name => !name.StartsWith('[') && name.Length >= SubsiteRules.NicknameMinLength)
            .Where(name => SubsiteRules.NormalizeNickname(name) is not null)
            .Distinct()
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.True(TopicCreationRules.IsReservedAddress(name), $"/{name} is not reserved"));
    }

    [Fact]
    public void NameNeedsTwoCharacters()
    {
        Assert.False(TopicCreationRules.IsLongEnoughName(" Я "));
        Assert.True(TopicCreationRules.IsLongEnoughName("ИИ"));
    }

    [Fact]
    public void LimitsHaveDefaults()
    {
        var limits = BlogsSettings.ReadLimits(Configuration(max: null, stopWords: null));

        Assert.Equal(TopicLimits.DefaultMaxPerAccount, limits.MaxPerAccount);
        Assert.Equal(TopicLimits.Default.StopWords, limits.StopWords);
    }

    [Fact]
    public void LimitsComeFromSettings()
    {
        var limits = BlogsSettings.ReadLimits(Configuration(max: "2", stopWords: "spam, реклама"));

        Assert.Equal(2, limits.MaxPerAccount);
        Assert.Equal(["spam", "реклама"], limits.StopWords);
    }

    [Fact]
    public void AnEmptyStopListIsAllowed() =>
        Assert.Empty(BlogsSettings.ReadLimits(Configuration(max: null, stopWords: "")).StopWords);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1001")]
    [InlineData("five")]
    public void AWrongNumberOfTopicsStopsTheHost(string max) =>
        Assert.Throws<InvalidOperationException>(() => BlogsSettings.ReadLimits(Configuration(max, stopWords: null)));

    private static IConfiguration Configuration(string? max, string? stopWords) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            [BlogsSettings.MaxTopicsPerAccountKey] = max,
            [BlogsSettings.StopWordsKey] = stopWords,
        })
        .Build();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "backend", "Platform.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("repository root (backend/Platform.slnx) not found");
    }
}
