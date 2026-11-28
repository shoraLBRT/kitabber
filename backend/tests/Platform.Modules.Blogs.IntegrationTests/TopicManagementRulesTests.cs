using Platform.Modules.Blogs.Topics;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>E11-12, ADR-0121: the rules, the roles, the moderators and the hand-over of a topic.</summary>
public sealed class TopicManagementRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _owner = Guid.CreateVersion7(_now);
    private static readonly Guid _other = Guid.CreateVersion7(_now.AddSeconds(1));

    [Fact]
    public void ATopicHasRulesUpToTheLimit()
    {
        var topic = Subsite.CreateTopic("Наука", "science", _owner, _now);

        topic.SetRules("  1. Без спама  ");
        Assert.Equal("1. Без спама", topic.Rules);
        topic.SetRules(new string('а', SubsiteRules.RulesMaxLength));
        Assert.Equal(SubsiteRules.RulesMaxLength, topic.Rules!.Length);
        topic.SetRules("   ");
        Assert.Null(topic.Rules);
        Assert.Throws<ArgumentException>(() => topic.SetRules(new string('а', SubsiteRules.RulesMaxLength + 1)));
    }

    [Fact]
    public void APersonalBlogHasNoRulesAndNeverChangesHands()
    {
        var blog = Subsite.CreatePersonal(_owner, _now);

        Assert.Throws<InvalidOperationException>(() => blog.SetRules("Правила"));
        Assert.Throws<InvalidOperationException>(() => blog.TransferTo(_other));
        Assert.Equal(_owner, blog.OwnerAccountId);
    }

    [Fact]
    public void ATopicChangesHandsToAnAccount()
    {
        var topic = Subsite.CreateTopic("Наука", "science", _owner, _now);

        topic.TransferTo(_other);

        Assert.Equal(_other, topic.OwnerAccountId);
        Assert.Throws<ArgumentException>(() => topic.TransferTo(Guid.Empty));
    }

    [Fact]
    public void TheRoleIsOwnerModeratorOrNone()
    {
        Assert.Equal(TopicRole.Owner, TopicManagementRules.RoleOf(_owner, _owner, isModerator: false));
        Assert.Equal(TopicRole.Moderator, TopicManagementRules.RoleOf(_other, _owner, isModerator: true));
        Assert.Equal(TopicRole.None, TopicManagementRules.RoleOf(_other, _owner, isModerator: false));
        Assert.Equal(TopicRole.None, TopicManagementRules.RoleOf(null, _owner, isModerator: false));
        Assert.Equal(
            ["owner", "moderator", "none"],
            new[] { TopicRole.Owner, TopicRole.Moderator, TopicRole.None }.Select(TopicManagementRules.NameOf));
    }

    [Fact]
    public void AModeratorIsNotTheOwnerNotAppointedTwiceAndWithinTheLimit()
    {
        var full = Enumerable.Range(0, TopicManagementRules.MaxModerators)
            .Select(i => Guid.CreateVersion7(_now.AddMinutes(i + 1))).ToList();

        Assert.Null(TopicManagementRules.ModeratorProblem(_other, _owner, []));
        Assert.NotNull(TopicManagementRules.ModeratorProblem(_owner, _owner, []));
        Assert.NotNull(TopicManagementRules.ModeratorProblem(_other, _owner, [_other]));
        Assert.NotNull(TopicManagementRules.ModeratorProblem(_other, _owner, full));
        Assert.Null(TopicManagementRules.ModeratorProblem(_other, _owner, full[1..]));
    }

    [Fact]
    public void ATopicIsOfferedOnlyToAnotherPerson()
    {
        Assert.Null(TopicManagementRules.TransferProblem(_other, _owner));
        Assert.NotNull(TopicManagementRules.TransferProblem(_owner, _owner));
    }
}
