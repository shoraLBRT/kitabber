using Microsoft.Extensions.Logging;
using Platform.Realtime;

namespace Platform.Modules.Reactions;

/// <summary>How many people put one kind of reaction on a target.</summary>
/// <param name="Kind">The reaction, as the client names it: <c>like</c>, <c>heart</c>, … (ADR-0101).</param>
/// <param name="Count">How many people put it.</param>
public sealed record ReactionCountView(string Kind, int Count);

/// <summary>
/// The realtime event <c>reactions.changed</c> in the topic <c>post:&lt;id&gt;</c> (E10-03, ADR-0072): the new counts
/// of a post or of one of its comments after someone put or took back a reaction. It names no one: whose reaction it
/// is, each reader knows from their own reads and changes.
/// </summary>
/// <param name="Target"><c>post</c> or <c>comment</c>.</param>
/// <param name="TargetId">The post or the comment.</param>
/// <param name="Reactions">Every kind on the target with its count; a kind nobody put is left out.</param>
public sealed record ReactionsChangedEvent(string Target, Guid TargetId, IReadOnlyList<ReactionCountView> Reactions);

/// <summary>Publishes the new counts of a target to everyone reading its post (ADR-0072).</summary>
internal static partial class ReactionEvents
{
    public const string ChangedType = "reactions.changed";

    public static string NameOf(ReactionTarget target) => target == ReactionTarget.Post ? "post" : "comment";

    public static string NameOf(ReactionKind kind) => ReactionKinds.CodeOf(kind);

    /// <summary>
    /// Sends the counts; a channel that cannot take the event (the backplane is down) does not undo the change, the
    /// readers simply see it on their next read.
    /// </summary>
    public static async Task PublishAsync(
        IRealtimePublisher publisher,
        ILogger logger,
        Guid postId,
        ReactionTarget target,
        Guid targetId,
        IReadOnlyList<ReactionCountView> counts,
        CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(
                RealtimeTopic.Post(postId), ChangedType, new ReactionsChangedEvent(NameOf(target), targetId, counts), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            NotPublished(logger, e, postId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The new reaction counts of post {PostId} were not published")]
    private static partial void NotPublished(ILogger logger, Exception exception, Guid postId);
}
