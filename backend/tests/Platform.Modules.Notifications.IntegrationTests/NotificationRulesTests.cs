using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Notifications.IntegrationTests;

/// <summary>E12-01 (FR-NOTIFY, ADR-0092): who an event notifies, apart from the database.</summary>
public sealed class NotificationRulesTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _postAuthor = Guid.CreateVersion7(_at.AddDays(-3));
    private static readonly Guid _parentAuthor = Guid.CreateVersion7(_at.AddDays(-2));
    private static readonly Guid _actor = Guid.CreateVersion7(_at.AddDays(-1));
    private static readonly Guid _mentioned = Guid.CreateVersion7(_at.AddHours(-1));

    [Fact]
    public void ReplyNotifiesTheParentAuthorAndThePostAuthorOnce()
    {
        var posted = Comment(parentAuthor: _parentAuthor, mentioned: [_mentioned, _parentAuthor]);

        var drafts = NotificationRules.ForComment(posted);

        Assert.Equal(
            [(_parentAuthor, NotificationType.CommentReply), (_postAuthor, NotificationType.PostComment),
                (_mentioned, NotificationType.Mention)],
            drafts.Select(d => (d.RecipientAccountId, d.Type)));
        Assert.All(drafts, d =>
        {
            Assert.Equal(_actor, d.ActorAccountId);
            Assert.Equal($"comment:{posted.CommentId:N}", d.SourceKey);
            Assert.Equal(posted.PostId, d.PostId);
            Assert.Equal(posted.CommentId, d.CommentId);
            Assert.Equal(_at, d.OccurredAt);
        });
    }

    [Fact]
    public void NobodyIsNotifiedAboutTheirOwnComment()
    {
        // The post's author replies to their own comment and mentions themselves.
        var posted = Comment(author: _postAuthor, parentAuthor: _postAuthor, mentioned: [_postAuthor]);

        Assert.Empty(NotificationRules.ForComment(posted));
    }

    [Fact]
    public void ReplyToTheAuthorsOwnCommentUnderTheirPostIsAReply()
    {
        var posted = Comment(parentAuthor: _postAuthor);

        var draft = Assert.Single(NotificationRules.ForComment(posted));
        Assert.Equal((_postAuthor, NotificationType.CommentReply), (draft.RecipientAccountId, draft.Type));
    }

    [Fact]
    public void ReactionNotifiesTheAuthorOncePerPersonAndTarget()
    {
        var comment = Guid.CreateVersion7(_at);
        var put = new ReactionPut("comment", comment, Guid.CreateVersion7(_at), _parentAuthor, _actor, "like", _at);

        var draft = Assert.Single(NotificationRules.ForReaction(put));

        Assert.Equal(NotificationType.Reaction, draft.Type);
        Assert.Equal(_parentAuthor, draft.RecipientAccountId);
        Assert.Equal(comment, draft.CommentId);
        Assert.Equal(put.PostId, draft.PostId);
        Assert.Equal("like", draft.ReactionKind);
        // The key does not name the reaction: changing it, or putting it again, is the same action.
        Assert.Equal(
            draft.SourceKey,
            Assert.Single(NotificationRules.ForReaction(put with { Kind = "other", OccurredAt = _at.AddHours(1) })).SourceKey);
        Assert.Empty(NotificationRules.ForReaction(put with { AccountId = _parentAuthor }));
    }

    [Fact]
    public void ReactionOnAPostNamesNoComment()
    {
        var post = Guid.CreateVersion7(_at);

        var draft = Assert.Single(NotificationRules.ForReaction(
            new ReactionPut("post", post, post, _postAuthor, _actor, "like", _at)));

        Assert.Equal(post, draft.PostId);
        Assert.Null(draft.CommentId);
    }

    [Fact]
    public void OnlyASubscriptionToAPersonalBlogNotifiesItsOwner()
    {
        var blog = Guid.CreateVersion7(_at);
        var subscribed = new BlogSubscribed(blog, BlogKind.Personal, _postAuthor, _actor, _at);

        var draft = Assert.Single(NotificationRules.ForSubscription(subscribed));
        Assert.Equal((_postAuthor, NotificationType.NewSubscriber, _actor, blog), (draft.RecipientAccountId, draft.Type,
            draft.ActorAccountId, draft.BlogId));

        Assert.Empty(NotificationRules.ForSubscription(subscribed with { Kind = BlogKind.Topic }));
        Assert.Empty(NotificationRules.ForSubscription(subscribed with { OwnerAccountId = null }));
        Assert.Empty(NotificationRules.ForSubscription(subscribed with { SubscriberAccountId = _postAuthor }));
    }

    // E10-08, ADR-0117.
    [Fact]
    public void RepostNotifiesTheOriginalsAuthorOncePerReposter()
    {
        var post = Guid.CreateVersion7(_at.AddDays(-3));
        var reposted = new PostReposted(Guid.CreateVersion7(_at), post, _postAuthor, _actor, _at);

        var draft = Assert.Single(NotificationRules.ForRepost(reposted));
        Assert.Equal((_postAuthor, NotificationType.Repost, _actor, post), (draft.RecipientAccountId, draft.Type,
            draft.ActorAccountId, draft.PostId));
        Assert.Equal(_at, draft.OccurredAt);

        // Another repost of the same post by the same person, after the first was deleted, is the same action.
        var again = new PostReposted(Guid.CreateVersion7(_at.AddHours(1)), post, _postAuthor, _actor, _at.AddHours(1));
        Assert.Equal(draft.SourceKey, Assert.Single(NotificationRules.ForRepost(again)).SourceKey);
        Assert.Empty(NotificationRules.ForRepost(reposted with { ReposterAccountId = _postAuthor }));
    }

    private static CommentPosted Comment(
        Guid? author = null,
        Guid? parentAuthor = null,
        IReadOnlyList<Guid>? mentioned = null) =>
        new(
            Guid.CreateVersion7(_at),
            Guid.CreateVersion7(_at.AddDays(-3)),
            _postAuthor,
            author ?? _actor,
            parentAuthor is null ? null : Guid.CreateVersion7(_at.AddMinutes(-5)),
            parentAuthor,
            mentioned ?? [],
            _at);
}
