using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Notifications;

/// <summary>A notification the rules ask for, before the black list and the repeats are checked.</summary>
/// <param name="RecipientAccountId">Who is notified.</param>
/// <param name="Type">What about.</param>
/// <param name="ActorAccountId">Who acted.</param>
/// <param name="SourceKey">The action; one action notifies one person once.</param>
/// <param name="OccurredAt">When the action happened.</param>
/// <param name="PostId">The post, when there is one.</param>
/// <param name="CommentId">The comment, when there is one.</param>
/// <param name="BlogId">The blog, for a subscription.</param>
/// <param name="ReactionKind">The reaction, for a reaction.</param>
public sealed record NotificationDraft(
    Guid RecipientAccountId,
    NotificationType Type,
    Guid ActorAccountId,
    string SourceKey,
    DateTimeOffset OccurredAt,
    Guid? PostId = null,
    Guid? CommentId = null,
    Guid? BlogId = null,
    string? ReactionKind = null);

/// <summary>
/// Who is notified about an event, apart from the database (ADR-0092). Nobody is notified about their own action, and
/// one action notifies a person once: a comment that is a reply to the recipient, under the recipient's post and
/// mentioning the recipient is one notification — a reply first, then a comment on the post, then a mention.
/// </summary>
public static class NotificationRules
{
    /// <summary>The notifications of a new comment: its parent's author, the post's author, the people it mentions.</summary>
    public static IReadOnlyList<NotificationDraft> ForComment(CommentPosted posted)
    {
        ArgumentNullException.ThrowIfNull(posted);
        var key = $"comment:{posted.CommentId:N}";
        var notified = new HashSet<Guid> { posted.AuthorAccountId };
        var drafts = new List<NotificationDraft>();

        void Add(Guid? recipient, NotificationType type)
        {
            if (recipient is { } account && notified.Add(account))
            {
                drafts.Add(new NotificationDraft(
                    account, type, posted.AuthorAccountId, key, posted.OccurredAt, posted.PostId, posted.CommentId));
            }
        }

        Add(posted.ParentAuthorAccountId, NotificationType.CommentReply);
        Add(posted.PostAuthorAccountId, NotificationType.PostComment);
        foreach (var mentioned in posted.MentionedAccountIds ?? [])
        {
            Add(mentioned, NotificationType.Mention);
        }

        return drafts;
    }

    /// <summary>
    /// The notification of a reaction: the author of the post or the comment. The key is the person and the target,
    /// whatever the reaction, so taking a reaction back and putting it again, or changing it, notifies once.
    /// </summary>
    public static IReadOnlyList<NotificationDraft> ForReaction(ReactionPut put)
    {
        ArgumentNullException.ThrowIfNull(put);
        if (put.TargetAuthorAccountId == put.AccountId)
        {
            return [];
        }

        var onComment = string.Equals(put.Target, "comment", StringComparison.Ordinal);
        return
        [
            new NotificationDraft(
                put.TargetAuthorAccountId,
                NotificationType.Reaction,
                put.AccountId,
                $"reaction:{put.Target}:{put.TargetId:N}:{put.AccountId:N}",
                put.OccurredAt,
                PostId: put.PostId,
                CommentId: onComment ? put.TargetId : null,
                ReactionKind: put.Kind),
        ];
    }

    /// <summary>
    /// The notification of a repost (E10-08, ADR-0117): the original's author, about their post. The key is the post and
    /// the reposter, so deleting a repost and reposting again notifies once.
    /// </summary>
    public static IReadOnlyList<NotificationDraft> ForRepost(PostReposted reposted)
    {
        ArgumentNullException.ThrowIfNull(reposted);
        if (reposted.PostAuthorAccountId == reposted.ReposterAccountId)
        {
            return [];
        }

        return
        [
            new NotificationDraft(
                reposted.PostAuthorAccountId,
                NotificationType.Repost,
                reposted.ReposterAccountId,
                $"repost:{reposted.PostId:N}:{reposted.ReposterAccountId:N}",
                reposted.OccurredAt,
                PostId: reposted.PostId),
        ];
    }

    /// <summary>
    /// The notification of a subscription: the owner of a personal blog learns about a new subscriber, once per
    /// subscriber. A subscription to a topic notifies no one (ADR-0092, a proposal).
    /// </summary>
    public static IReadOnlyList<NotificationDraft> ForSubscription(BlogSubscribed subscribed)
    {
        ArgumentNullException.ThrowIfNull(subscribed);
        if (subscribed.Kind != BlogKind.Personal
            || subscribed.OwnerAccountId is not { } owner
            || owner == subscribed.SubscriberAccountId)
        {
            return [];
        }

        return
        [
            new NotificationDraft(
                owner,
                NotificationType.NewSubscriber,
                subscribed.SubscriberAccountId,
                $"subscriber:{subscribed.BlogId:N}:{subscribed.SubscriberAccountId:N}",
                subscribed.OccurredAt,
                BlogId: subscribed.BlogId),
        ];
    }
}
