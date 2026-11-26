using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Messaging;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Identity.Contracts;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Notifications;

/// <summary>A new comment notifies its parent's author, the post's author and the people it mentions (ADR-0092).</summary>
public sealed class CommentPostedNotificationsConsumer(NotificationWriter writer) : IConsumer<CommentPosted>
{
    public Task Consume(ConsumeContext<CommentPosted> context) =>
        writer.AddAsync(NotificationRules.ForComment(context.Message), context.CancellationToken);
}

/// <summary>The inbox of the Notifications schema for <see cref="CommentPostedNotificationsConsumer"/> (ADR-0005).</summary>
public sealed class CommentPostedNotificationsConsumerDefinition
    : ModuleConsumerDefinition<CommentPostedNotificationsConsumer, NotificationsDbContext>;

/// <summary>A reaction notifies the author of the post or the comment (ADR-0092).</summary>
public sealed class ReactionPutNotificationsConsumer(NotificationWriter writer) : IConsumer<ReactionPut>
{
    public Task Consume(ConsumeContext<ReactionPut> context) =>
        writer.AddAsync(NotificationRules.ForReaction(context.Message), context.CancellationToken);
}

/// <summary>The inbox of the Notifications schema for <see cref="ReactionPutNotificationsConsumer"/> (ADR-0005).</summary>
public sealed class ReactionPutNotificationsConsumerDefinition
    : ModuleConsumerDefinition<ReactionPutNotificationsConsumer, NotificationsDbContext>;

/// <summary>A subscription to a personal blog notifies its owner (ADR-0092).</summary>
public sealed class BlogSubscribedNotificationsConsumer(NotificationWriter writer) : IConsumer<BlogSubscribed>
{
    public Task Consume(ConsumeContext<BlogSubscribed> context) =>
        writer.AddAsync(NotificationRules.ForSubscription(context.Message), context.CancellationToken);
}

/// <summary>The inbox of the Notifications schema for <see cref="BlogSubscribedNotificationsConsumer"/> (ADR-0005).</summary>
public sealed class BlogSubscribedNotificationsConsumerDefinition
    : ModuleConsumerDefinition<BlogSubscribedNotificationsConsumer, NotificationsDbContext>;

/// <summary>
/// Erases the notifications of a deleted account (FR-ID-5, ADR-0087): those it received and those its actions caused.
/// A repeat finds nothing and does nothing.
/// </summary>
public sealed class AccountDeletedNotificationsConsumer(NotificationsDbContext db) : IConsumer<AccountDeleted>
{
    public async Task Consume(ConsumeContext<AccountDeleted> context)
    {
        var account = context.Message.AccountId;
        var gone = await db.Notifications
            .Where(n => n.RecipientAccountId == account || n.ActorAccountId == account)
            .ToListAsync(context.CancellationToken);
        db.Notifications.RemoveRange(gone);
    }
}

/// <summary>The inbox of the Notifications schema for <see cref="AccountDeletedNotificationsConsumer"/> (ADR-0005).</summary>
public sealed class AccountDeletedNotificationsConsumerDefinition
    : ModuleConsumerDefinition<AccountDeletedNotificationsConsumer, NotificationsDbContext>;
