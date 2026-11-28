using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Modules.Notifications.Contracts;

namespace Platform.Modules.Notifications;

/// <summary>
/// Adds the notifications the rules ask for (ADR-0092) to the context, without saving: the consumer's inbox saves them
/// with its record (<see cref="Platform.Messaging.ModuleConsumerDefinition{TConsumer, TContext}"/>). A draft is left
/// out when a source of <see cref="INotificationSuppression"/> suppresses its actor for its recipient, or when the
/// recipient already has a notification of the same action — a repeated event, even one with a new message id, adds
/// nothing. Two events of the same action at the same moment meet the unique key; the bus retries the loser, which
/// then finds the notification there.
/// </summary>
public sealed class NotificationWriter(
    NotificationsDbContext db,
    IEnumerable<INotificationSuppression> suppressions,
    TimeProvider time)
{
    /// <summary>Adds what is not left out and gives the accounts that got a new notification, each once.</summary>
    public async Task<IReadOnlyList<Guid>> AddAsync(IReadOnlyList<NotificationDraft> drafts, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var added = new List<Guid>();
        foreach (var draft in drafts)
        {
            if (await SuppressedAsync(draft, ct))
            {
                continue;
            }

            var there = await db.Notifications.AnyAsync(
                n => n.RecipientAccountId == draft.RecipientAccountId && n.SourceKey == draft.SourceKey, ct);
            if (there)
            {
                continue;
            }

            db.Notifications.Add(new Notification
            {
                Id = Guid.CreateVersion7(time.GetUtcNow()),
                RecipientAccountId = draft.RecipientAccountId,
                Type = draft.Type,
                ActorAccountId = draft.ActorAccountId,
                PostId = draft.PostId,
                CommentId = draft.CommentId,
                BlogId = draft.BlogId,
                ReactionKind = draft.ReactionKind,
                SourceKey = draft.SourceKey,
                CreatedAt = draft.OccurredAt,
            });
            if (!added.Contains(draft.RecipientAccountId))
            {
                added.Add(draft.RecipientAccountId);
            }
        }

        return added;
    }

    /// <summary>
    /// Adds the drafts inside a consumer and, when anyone got a new notification, publishes
    /// <see cref="NotificationsArrived"/> through the consumer's outbox: it leaves with the rows' commit (ADR-0123).
    /// </summary>
    public async Task AddAndAnnounceAsync(ConsumeContext context, IReadOnlyList<NotificationDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(context);
        var recipients = await AddAsync(drafts, context.CancellationToken);
        if (recipients.Count > 0)
        {
            await context.Publish(new NotificationsArrived(recipients), context.CancellationToken);
        }
    }

    private async Task<bool> SuppressedAsync(NotificationDraft draft, CancellationToken ct)
    {
        foreach (var suppression in suppressions)
        {
            var suppressed = await suppression.SuppressedActorsAsync(
                draft.RecipientAccountId, [draft.ActorAccountId], ct);
            if (suppressed.Contains(draft.ActorAccountId))
            {
                return true;
            }
        }

        return false;
    }
}
