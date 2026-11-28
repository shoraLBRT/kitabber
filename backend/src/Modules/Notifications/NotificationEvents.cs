using MassTransit;
using Microsoft.Extensions.Logging;
using Platform.Realtime;

namespace Platform.Modules.Notifications;

/// <summary>
/// Notifications were added for these accounts (E12-04, ADR-0123). A consumer that adds them publishes it through its
/// own outbox, so it leaves only once the rows are committed; <see cref="NotificationsArrivedRealtimeConsumer"/> then
/// tells the recipients' open pages. Internal to the module: no other module consumes it.
/// </summary>
/// <param name="RecipientAccountIds">The accounts that got at least one new notification, each once.</param>
public sealed record NotificationsArrived(IReadOnlyList<Guid> RecipientAccountIds);

/// <summary>
/// The realtime event <c>notifications.changed</c> in the topic <c>account:&lt;id&gt;</c> (E12-04, ADR-0123): the
/// account's notifications changed — a new one came or some were marked read in another tab. It carries nothing: every
/// open page of the account asks for the unread number again, so whatever order the events come in, the last answer is
/// the number the server has.
/// </summary>
internal static partial class NotificationEvents
{
    public const string ChangedType = "notifications.changed";

    /// <summary>
    /// Sends the event to the account's pages; a channel that cannot take it (the backplane is down) undoes nothing:
    /// the pages see the number on their next read or after they reconnect.
    /// </summary>
    public static async Task PublishChangedAsync(
        IRealtimePublisher publisher, ILogger logger, Guid account, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(RealtimeTopic.Account(account), ChangedType, null, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            NotPublished(logger, e, account);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The change of the notifications of {AccountId} was not sent to its pages")]
    private static partial void NotPublished(ILogger logger, Exception exception, Guid accountId);
}

/// <summary>
/// Tells the open pages of each recipient that their notifications changed (ADR-0123). It changes nothing in the
/// database and keeps no inbox: a repeated message only makes the pages ask for the number once more.
/// </summary>
public sealed class NotificationsArrivedRealtimeConsumer(
    IRealtimePublisher realtime, ILogger<NotificationsArrivedRealtimeConsumer> logger) : IConsumer<NotificationsArrived>
{
    public async Task Consume(ConsumeContext<NotificationsArrived> context)
    {
        foreach (var recipient in context.Message.RecipientAccountIds.Distinct())
        {
            await NotificationEvents.PublishChangedAsync(realtime, logger, recipient, context.CancellationToken);
        }
    }
}
