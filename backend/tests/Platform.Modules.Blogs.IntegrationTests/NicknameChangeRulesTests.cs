using Platform.Modules.Blogs.Topics;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>E04-05, ADR-0090: the nickname of a personal blog changes once in 30 days; the first one is free.</summary>
public sealed class NicknameChangeRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static Subsite NewBlog() => Subsite.CreatePersonal(Guid.CreateVersion7(_now), _now);

    [Fact]
    public void TheFirstNicknameIsFreeAndCanBeChangedRightAway()
    {
        var blog = NewBlog();

        Assert.Null(blog.ChangeNickname("@Anna", _now));

        Assert.Equal("anna", blog.Nickname);
        Assert.Null(blog.NicknameChangedAt);
        Assert.True(blog.CanChangeNicknameAt(_now));
    }

    [Fact]
    public void AChangeStartsThirtyDays()
    {
        var blog = NewBlog();
        blog.ChangeNickname("anna", _now);

        Assert.Equal("anna", blog.ChangeNickname("anna_k", _now));

        Assert.Equal(_now, blog.NicknameChangedAt);
        Assert.Equal(_now.AddDays(30), blog.NicknameChangeableFrom);
        Assert.False(blog.CanChangeNicknameAt(_now.AddDays(30).AddTicks(-1)));
        Assert.Throws<InvalidOperationException>(() => blog.ChangeNickname("anna_b", _now.AddDays(29)));
        Assert.Equal("anna_k", blog.Nickname);
        Assert.True(blog.CanChangeNicknameAt(_now.AddDays(30)));
        Assert.Equal("anna_k", blog.ChangeNickname("anna_b", _now.AddDays(30)));
        Assert.Equal(_now.AddDays(30), blog.NicknameChangedAt);
    }

    [Fact]
    public void TheSameNicknameIsNoChange()
    {
        var blog = NewBlog();
        blog.ChangeNickname("anna", _now);
        blog.ChangeNickname("anna_k", _now);

        Assert.Null(blog.ChangeNickname("ANNA_K", _now.AddDays(1)));

        Assert.Equal(_now, blog.NicknameChangedAt);
    }

    [Fact]
    public void ANicknameOutsideTheFormIsRefused() =>
        Assert.Throws<ArgumentException>(() => NewBlog().ChangeNickname("id123", _now));

    [Fact]
    public void AFormerNicknameIsHeldForNinetyDays()
    {
        var former = new FormerNickname { Nickname = "anna", SubsiteId = Guid.CreateVersion7(_now), ReleasedAt = _now };

        Assert.False(former.IsFreeAt(_now.AddDays(90).AddTicks(-1)));
        Assert.True(former.IsFreeAt(_now.AddDays(90)));
    }

    [Theory]
    [InlineData("settings")]
    [InlineData("popular")]
    [InlineData("admin_anna")]
    [InlineData("support")]
    public void ReservedAddressesAndTheStopListAreRefusedAsNicknames(string nickname) =>
        Assert.True(BlogAddresses.IsRefused(nickname, TopicLimits.Default));

    [Fact]
    public void AnOrdinaryNicknameIsNotRefused() => Assert.False(BlogAddresses.IsRefused("anna_k", TopicLimits.Default));
}
