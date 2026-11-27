namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>E03-10, ADR-0102: the rules of the onboarding apart from the database — the name and that it happens once.</summary>
public sealed class OnboardingRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Ян", true)]
    [InlineData("  Ян  ", true)]
    [InlineData("Я", false)]
    [InlineData("📚", false)]
    [InlineData("📚📚", true)]
    [InlineData("Двадцать пять символов!!!", true)]
    [InlineData("Двадцать шесть символов!!!", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void NameIsTwoToTwentyFiveCharactersPeopleSee(string? name, bool valid) =>
        Assert.Equal(valid, SubsiteRules.IsValidOnboardingName(name));

    [Fact]
    public void NewPersonalBlogIsOnboardedOnce()
    {
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(_now), _now);
        Assert.True(blog.OnboardingPending);

        Assert.True(blog.FinishOnboarding(_now));
        Assert.False(blog.OnboardingPending);
        Assert.Equal(_now, blog.OnboardedAt);

        Assert.False(blog.FinishOnboarding(_now.AddHours(1)));
        Assert.Equal(_now, blog.OnboardedAt);
    }

    [Fact]
    public void TopicHasNoOnboarding()
    {
        var owner = Guid.CreateVersion7(_now);
        var topic = Subsite.CreateTopic("Наука", "science", owner, _now);

        Assert.False(topic.OnboardingPending);
        Assert.False(topic.FinishOnboarding(_now));
    }

    [Fact]
    public void DemoBlogsAreOnboardedAlready()
    {
        var blog = new PersonalBlogFactory(new Platform.Seeding.SeedData(Platform.Seeding.SeedData.DefaultSeed, Platform.Testing.Time.TestClock.Create())).Build();

        Assert.False(blog.OnboardingPending);
    }
}
