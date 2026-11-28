namespace Platform.Modules.Notifications;

/// <summary>
/// What a notification tells about (FR-NOTIFY, ADR-0092). The set grows with the events of the spec — a new post in a
/// subscription, a repost, a post-reply, an achievement, a moderator's decision — each one a new value and a new rule
/// in <see cref="NotificationRules"/>; the stored name is the value's name, so a value is never renamed.
/// </summary>
public enum NotificationType
{
    /// <summary>Someone replied to the recipient's comment.</summary>
    CommentReply = 1,

    /// <summary>Someone commented on the recipient's post (not as a reply to the recipient's own comment).</summary>
    PostComment = 2,

    /// <summary>Someone put a reaction on the recipient's post or comment.</summary>
    Reaction = 3,

    /// <summary>Someone mentioned the recipient by <c>@ник</c>.</summary>
    Mention = 4,

    /// <summary>Someone subscribed to the recipient's personal blog.</summary>
    NewSubscriber = 5,

    /// <summary>Someone reposted the recipient's post into their blog (E10-08, ADR-0117).</summary>
    Repost = 6,

    /// <summary>A blog or a topic the recipient turned notifications on for got a new post (E11-04, ADR-0118).</summary>
    SubscriptionPost = 7,

    /// <summary>The owner of a topic offered it to the recipient, who accepts or declines (E11-12, ADR-0121).</summary>
    TopicTransferOffer = 8,
}

/// <summary>
/// A notification of one person about one action of another (FR-NOTIFY, ADR-0092). The recipient and the actor are
/// known only by their account_id (FR-ID-1); what the action was about — a post, a comment, a blog — only by its
/// identifier, read again from its module when the notification is shown (E12-02). <see cref="SourceKey"/> names the
/// action, so that one action never notifies the same person twice.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; init; }

    /// <summary>Who is notified.</summary>
    public Guid RecipientAccountId { get; init; }

    public NotificationType Type { get; init; }

    /// <summary>Who acted: the author of the comment, the person who reacted or subscribed.</summary>
    public Guid ActorAccountId { get; init; }

    /// <summary>The post the action is about or under; <c>null</c> for a subscription.</summary>
    public Guid? PostId { get; init; }

    /// <summary>The comment the action is or is about; <c>null</c> when it is about a post or a blog.</summary>
    public Guid? CommentId { get; init; }

    /// <summary>The blog subscribed to, or the blog of a new post in a subscription; <c>null</c> for the other types.</summary>
    public Guid? BlogId { get; init; }

    /// <summary>The reaction, as the client names it (<c>like</c>); <c>null</c> for the other types.</summary>
    public string? ReactionKind { get; init; }

    /// <summary>The action, the same for every repeat of its event (<see cref="NotificationRules"/>).</summary>
    public string SourceKey { get; init; } = string.Empty;

    /// <summary>When the action happened.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the recipient read it (E12-02, E12-03); <c>null</c> while unread.</summary>
    public DateTimeOffset? ReadAt { get; init; }

    /// <summary>
    /// The row of the feed the notification is shown in (E12-03, ADR-0122): the same for the reactions to one post or
    /// comment, the reposts of one post, the comments under one post and the new subscribers of one blog; the
    /// notification's own id for the other types. The database computes it from the other columns
    /// (<see cref="NotificationsDbContext.GroupKeySql"/>), so nothing writes it.
    /// </summary>
    public string GroupKey { get; private set; } = string.Empty;
}
