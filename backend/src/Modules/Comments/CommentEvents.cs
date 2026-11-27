using Microsoft.Extensions.Logging;
using Platform.Realtime;

namespace Platform.Modules.Comments;

/// <summary>
/// The realtime event <c>comments.posted</c> in the topic <c>post:&lt;id&gt;</c> (E09-09, ADR-0112): a new comment
/// under the post someone is reading. It carries only where the comment stands and names no one: each reader reads the
/// comment itself through the API, which folds the authors they blocked (ADR-0091), so one event serves every reader.
/// </summary>
/// <param name="CommentId">The new comment.</param>
/// <param name="PostId">The post it is under.</param>
/// <param name="ParentId">The comment it replies to; <c>null</c> for a reply to the post.</param>
public sealed record CommentPostedEvent(Guid CommentId, Guid PostId, Guid? ParentId);

/// <summary>Tells the readers of a post about its new comment (ADR-0112).</summary>
internal static partial class CommentEvents
{
    public const string PostedType = "comments.posted";

    /// <summary>
    /// Sends the event once the comment is kept; a channel that cannot take it (the backplane is down) does not undo the
    /// comment: the readers see it on their next read.
    /// </summary>
    public static async Task PublishPostedAsync(
        IRealtimePublisher publisher, ILogger logger, Comment comment, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(
                RealtimeTopic.Post(comment.PostId),
                PostedType,
                new CommentPostedEvent(comment.Id, comment.PostId, comment.ParentId),
                ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            NotPublished(logger, e, comment.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The new comment {CommentId} was not published to its readers")]
    private static partial void NotPublished(ILogger logger, Exception exception, Guid commentId);
}
