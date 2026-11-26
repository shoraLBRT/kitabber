namespace Platform.Modules.Notifications.Contracts;

/// <summary>
/// A source of the people whose actions must not notify a recipient (ADR-0092), such as the black list (E11-06): the
/// module that owns the list implements it over its own tables and registers it in the workers host. The Notifications
/// module asks every registered source before it writes a notification and writes none when any of them suppresses
/// it. With no source, nothing is suppressed.
/// </summary>
public interface INotificationSuppression
{
    /// <summary>
    /// The actors of <paramref name="actorAccountIds"/> whose actions must not notify
    /// <paramref name="recipientAccountId"/>; an empty set when none.
    /// </summary>
    Task<IReadOnlySet<Guid>> SuppressedActorsAsync(
        Guid recipientAccountId,
        IReadOnlyCollection<Guid> actorAccountIds,
        CancellationToken cancellationToken);
}
