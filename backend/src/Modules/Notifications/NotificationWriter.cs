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
    public async Task<int> AddAsync(IReadOnlyList<NotificationDraft> drafts, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var added = 0;
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
            added++;
        }

        return added;
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
