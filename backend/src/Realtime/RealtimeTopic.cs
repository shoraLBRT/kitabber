using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace Platform.Realtime;

/// <summary>
/// What a realtime event is about, and so who receives it: <c>account:&lt;id&gt;</c> (one person, on every device)
/// or <c>post:&lt;id&gt;</c> (everyone reading the post). A topic is a SignalR group of that name. A module adds a kind
/// of its own by registering an <see cref="IRealtimeTopicPolicy"/> for it.
/// </summary>
public readonly record struct RealtimeTopic(string Kind, Guid Id)
{
    public const string AccountKind = "account";
    public const string PostKind = "post";

    /// <summary>The group name, for example <c>post:0193...</c>; the id is always in lowercase <c>D</c> form.</summary>
    public string Name => $"{Kind}:{Id:D}";

    public static RealtimeTopic Account(Guid accountId) => new(AccountKind, accountId);

    public static RealtimeTopic Post(Guid postId) => new(PostKind, postId);

    /// <summary>Reads <c>kind:uuid</c>; the kind is lowercase Latin letters.</summary>
    public static bool TryParse([NotNullWhen(true)] string? value, out RealtimeTopic topic)
    {
        topic = default;
        var colon = value?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        if (colon <= 0 || colon > 32)
        {
            return false;
        }

        var kind = value![..colon];
        if (!kind.All(c => c is >= 'a' and <= 'z') || !Guid.TryParseExact(value[(colon + 1)..], "D", out var id))
        {
            return false;
        }

        topic = new RealtimeTopic(kind, id);
        return true;
    }

    public override string ToString() => Name;
}

/// <summary>Who may subscribe to the topics of one kind. The hub refuses a kind without a policy.</summary>
public interface IRealtimeTopicPolicy
{
    /// <summary>The kind this policy decides, for example <c>post</c>. The last registered policy of a kind wins.</summary>
    string Kind { get; }

    ValueTask<bool> CanSubscribeAsync(ClaimsPrincipal user, RealtimeTopic topic, CancellationToken cancellationToken);
}

/// <summary>Only the account itself: nobody listens to another person's notifications.</summary>
internal sealed class AccountTopicPolicy : IRealtimeTopicPolicy
{
    public string Kind => RealtimeTopic.AccountKind;

    public ValueTask<bool> CanSubscribeAsync(ClaimsPrincipal user, RealtimeTopic topic, CancellationToken cancellationToken) =>
        ValueTask.FromResult(RealtimeAccounts.Of(user) == topic.Id);
}

/// <summary>
/// Any signed-in reader: published posts are public. The Posts module replaces this policy with one that checks the
/// post is visible to the reader (drafts, hidden posts) when it exists.
/// </summary>
internal sealed class PostTopicPolicy : IRealtimeTopicPolicy
{
    public string Kind => RealtimeTopic.PostKind;

    public ValueTask<bool> CanSubscribeAsync(ClaimsPrincipal user, RealtimeTopic topic, CancellationToken cancellationToken) =>
        ValueTask.FromResult(RealtimeAccounts.Of(user) is not null);
}

/// <summary>The account of a signed-in principal: <c>sub</c> of the provider's token (ADR-0018) or its mapped form.</summary>
public static class RealtimeAccounts
{
    public const string SubjectClaim = "sub";

    public static Guid? Of(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var value = (user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst(SubjectClaim))?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
