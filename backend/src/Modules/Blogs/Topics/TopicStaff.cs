namespace Platform.Modules.Blogs.Topics;

/// <summary>
/// A moderator of a topic (E11-12, ADR-0121): an account its owner appointed. Only the role is kept here; what a
/// moderator may do in the topic comes with E11-09 (#174). The owner is never a moderator of the own topic.
/// </summary>
public sealed class TopicModerator
{
    /// <summary>The topic.</summary>
    public Guid SubsiteId { get; init; }

    /// <summary>The account appointed, known only by its account_id (FR-ID-1).</summary>
    public Guid AccountId { get; init; }

    /// <summary>When the owner appointed it.</summary>
    public DateTimeOffset AppointedAt { get; init; }
}

/// <summary>
/// The owner's offer to hand a topic to another account (E11-12, ADR-0121). The topic changes hands only when that
/// account accepts; until then the owner may cancel the offer and the recipient may decline it. A topic has at most one
/// offer: a new one replaces it.
/// </summary>
public sealed class TopicTransferOffer
{
    /// <summary>The topic; one offer per topic.</summary>
    public Guid SubsiteId { get; init; }

    /// <summary>The owner who made the offer.</summary>
    public Guid FromAccountId { get; init; }

    /// <summary>The account the topic is offered to.</summary>
    public Guid ToAccountId { get; init; }

    /// <summary>When the offer was made.</summary>
    public DateTimeOffset OfferedAt { get; init; }
}

/// <summary>Who a person is in a topic (E11-12, ADR-0121).</summary>
public enum TopicRole
{
    /// <summary>A reader: neither the owner nor a moderator.</summary>
    None = 0,

    /// <summary>A moderator the owner appointed.</summary>
    Moderator = 1,

    /// <summary>The owner: edits the topic, appoints moderators, hands the topic over.</summary>
    Owner = 2,
}

/// <summary>The rules of running a topic (E11-12, ADR-0121).</summary>
public static class TopicManagementRules
{
    /// <summary>The most moderators a topic has: enough for a large community, few enough to list on one screen.</summary>
    public const int MaxModerators = 20;

    /// <summary>The role of <paramref name="accountId"/> in a topic owned by <paramref name="ownerAccountId"/>.</summary>
    public static TopicRole RoleOf(Guid? accountId, Guid? ownerAccountId, bool isModerator) =>
        accountId is null ? TopicRole.None
        : accountId == ownerAccountId ? TopicRole.Owner
        : isModerator ? TopicRole.Moderator
        : TopicRole.None;

    /// <summary>The name a role has in the API.</summary>
    public static string NameOf(TopicRole role) => role switch
    {
        TopicRole.Owner => "owner",
        TopicRole.Moderator => "moderator",
        _ => "none",
    };

    /// <summary>
    /// Why <paramref name="candidate"/> cannot be made a moderator, or <c>null</c> when it can: the owner is not a
    /// moderator of the own topic, an account is appointed once, and a topic has at most <see cref="MaxModerators"/>.
    /// </summary>
    public static string? ModeratorProblem(Guid candidate, Guid owner, IReadOnlyCollection<Guid> moderators)
    {
        ArgumentNullException.ThrowIfNull(moderators);
        if (candidate == owner)
        {
            return "Владелец темы не может быть её модератором.";
        }

        if (moderators.Contains(candidate))
        {
            return "Этот человек уже модератор темы.";
        }

        return moderators.Count >= MaxModerators ? $"У темы не больше {MaxModerators} модераторов." : null;
    }

    /// <summary>Why the topic cannot be offered to <paramref name="recipient"/>, or <c>null</c> when it can.</summary>
    public static string? TransferProblem(Guid recipient, Guid owner) =>
        recipient == owner ? "Тема уже ваша: выберите другого человека." : null;
}
