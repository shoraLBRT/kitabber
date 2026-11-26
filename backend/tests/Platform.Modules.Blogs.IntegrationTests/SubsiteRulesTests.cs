namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>The rules of a blog (FR-PROFILE): the type of a personal blog and the limits of its profile.</summary>
public sealed class SubsiteRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PersonalBlogBelongsToItsAccountAndStartsWithAnEmptyProfile()
    {
        var owner = Guid.CreateVersion7(_now);

        var blog = Subsite.CreatePersonal(owner, _now);

        Assert.Equal(SubsiteType.Personal, blog.Type);
        Assert.Equal(owner, blog.OwnerAccountId);
        Assert.Equal(_now, blog.CreatedAt);
        Assert.Equal(7, blog.Id.Version);
        Assert.Null(blog.Name);
        Assert.Null(blog.Description);
        Assert.Null(blog.Nickname);
    }

    [Fact]
    public void PersonalBlogNeedsAnOwner() =>
        Assert.Throws<ArgumentException>(() => Subsite.CreatePersonal(Guid.Empty, _now));

    [Fact]
    public void NameIsAtMost25Characters()
    {
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(_now), _now);

        blog.Rename(new string('я', 25));
        Assert.Equal(25, blog.Name!.Length);
        Assert.Throws<ArgumentException>(() => blog.Rename(new string('я', 26)));
        Assert.Equal(25, blog.Name.Length);
    }

    [Fact]
    public void CoverStartsInTheMiddleAndKeepsItsPositionFrom0To100()
    {
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(_now), _now);
        var cover = Guid.CreateVersion7(_now);
        Assert.Null(blog.CoverMediaId);
        Assert.Equal(50, blog.CoverPosition);

        blog.SetCover(cover, 0);
        Assert.Equal(cover, blog.CoverMediaId);
        Assert.Equal(0, blog.CoverPosition);
        blog.SetCover(cover, 100);
        Assert.Equal(100, blog.CoverPosition);
        Assert.Throws<ArgumentOutOfRangeException>(() => blog.SetCover(cover, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => blog.SetCover(cover, -1));
        Assert.Throws<ArgumentException>(() => blog.SetCover(Guid.Empty, 50));
        Assert.Equal(100, blog.CoverPosition);

        blog.RemoveCover();
        Assert.Null(blog.CoverMediaId);
        Assert.Equal(50, blog.CoverPosition);
    }

    [Fact]
    public void DescriptionIsAtMost160Characters()
    {
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(_now), _now);

        blog.Describe(new string('d', 160));
        Assert.Equal(160, blog.Description!.Length);
        Assert.Throws<ArgumentException>(() => blog.Describe(new string('d', 161)));
    }

    [Fact]
    public void CharactersAreCountedAsPeopleSeeThem()
    {
        // 25 emoji are 50 UTF-16 units and still 25 characters; a letter with a combining accent is one.
        var emoji = string.Concat(Enumerable.Repeat("😀", 25));
        var accented = string.Concat(Enumerable.Repeat("е́", 25));

        Assert.True(SubsiteRules.IsValidName(emoji));
        Assert.True(SubsiteRules.IsValidName(accented));
        Assert.False(SubsiteRules.IsValidName(emoji + "😀"));
    }

    [Fact]
    public void SurroundingSpacesDoNotCountAndBlankClears()
    {
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(_now), _now);

        blog.Rename("  " + new string('a', 25) + "  ");
        Assert.Equal(new string('a', 25), blog.Name);
        blog.Rename("   ");
        Assert.Null(blog.Name);
        blog.Describe("");
        Assert.Null(blog.Description);
    }

    [Fact]
    public void AvatarIsSetAndRemovedButNeverAnEmptyId()
    {
        var blog = Subsite.CreatePersonal(Guid.CreateVersion7(_now), _now);
        var image = Guid.CreateVersion7(_now);

        blog.SetAvatar(image);
        Assert.Equal(image, blog.AvatarMediaId);
        blog.SetAvatar(null);
        Assert.Null(blog.AvatarMediaId);
        Assert.Throws<ArgumentException>(() => blog.SetAvatar(Guid.Empty));
    }

    [Theory]
    [InlineData("reader", "reader")]
    [InlineData("@Reader_42", "reader_42")]
    [InlineData("  abc  ", "abc")]
    [InlineData("ab", null)]
    [InlineData("1reader", null)]
    [InlineData("читатель", null)]
    [InlineData("read-er", null)]
    [InlineData("id123", null)]
    [InlineData("idea", "idea")]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345", "abcdefghijklmnopqrstuvwxyz012345")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456", null)]
    [InlineData(null, null)]
    public void NicknameHasOneForm(string? typed, string? stored) =>
        Assert.Equal(stored, SubsiteRules.NormalizeNickname(typed));
}
