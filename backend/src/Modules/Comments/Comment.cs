namespace Platform.Modules.Comments;

/// <summary>Who wrote a comment (ADR-0048, FR-TG-6).</summary>
public enum CommentKind
{
    /// <summary>An account of the platform: the comment has its <c>account_id</c>.</summary>
    Native = 1,

    /// <summary>
    /// An outside author, such as a member of a Telegram channel's discussion group carried over by the bridge (E19):
    /// no account, only a name and a link. No one replies to a guest comment on the platform.
    /// </summary>
    Guest = 2,
}

/// <summary>The outside author of a guest comment (FR-TG-6).</summary>
/// <param name="Name">The name shown instead of a blog.</param>
/// <param name="Url">Where the name leads: the profile or the message in the outside service; may be missing.</param>
public sealed record GuestAuthor(string Name, Uri? Url);

/// <summary>The outside message a guest comment was carried over from: the pair makes the import idempotent.</summary>
/// <param name="Channel">The channel or group in the outside service.</param>
/// <param name="Message">The message in it.</param>
public sealed record CommentSource(string Channel, string Message);

/// <summary>
/// A comment under a post (FR-COMMENT, ADR-0048): a reply to the post or to another comment of the same post. Its
/// place in the tree is the materialized <see cref="Path"/> — the identifiers of its ancestors and its own, root first —
/// so a whole tree or one branch is read with one query ordered by the path. The post and the author are known only by
/// their identifiers (FR-ID-1).
/// </summary>
public sealed class Comment
{
    public Guid Id { get; init; }

    public Guid PostId { get; init; }

    /// <summary>The comment this one replies to; <c>null</c> for a reply to the post itself.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>The materialized path (<see cref="CommentPath"/>); its order is the order of the tree.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>The level in the tree: 1 for a reply to the post, the parent's depth plus one for a reply to a comment.</summary>
    public int Depth { get; init; }

    public CommentKind Kind { get; init; } = CommentKind.Native;

    /// <summary>The author of a native comment; <c>null</c> for a guest comment.</summary>
    public Guid? AuthorAccountId { get; init; }

    /// <summary>The name of a guest author; <c>null</c> for a native comment.</summary>
    public string? GuestName { get; init; }

    /// <summary>Where the guest author's name leads; may be missing.</summary>
    public string? GuestUrl { get; init; }

    /// <summary>The outside channel a guest comment came from; <c>null</c> for a native comment.</summary>
    public string? SourceChannel { get; init; }

    /// <summary>The message in <see cref="SourceChannel"/>.</summary>
    public string? SourceMessage { get; init; }

    /// <summary>The text, trimmed (<see cref="CommentRules.CleanText"/>); empty once the comment is deleted.</summary>
    public string Text { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the author last changed the text (E09-05, ADR-0059); <c>null</c> for a text never changed.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>
    /// When the author deleted it (E09-05, ADR-0059). A deleted comment stays only while it has replies, as the stub
    /// «Комментарий удалён» that holds their place in the tree; its text is gone.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>The guest author; <c>null</c> for a native comment.</summary>
    public GuestAuthor? Guest => Kind == CommentKind.Guest && GuestName is not null
        ? new GuestAuthor(GuestName, GuestUrl is null ? null : new Uri(GuestUrl))
        : null;

    /// <summary>
    /// A new comment by the account <paramref name="authorAccountId"/> under <paramref name="postId"/>, replying to
    /// <paramref name="parent"/> or, when it is <c>null</c>, to the post. Whether the reply is allowed (the depth, a
    /// guest parent) is <see cref="CommentRules.ReplyProblem"/>, checked by the endpoint.
    /// </summary>
    public static Comment CreateNative(Guid authorAccountId, Guid postId, Comment? parent, string text, DateTimeOffset now)
    {
        if (authorAccountId == Guid.Empty)
        {
            throw new ArgumentException("A native comment needs its author's account_id.", nameof(authorAccountId));
        }

        return Create(Guid.CreateVersion7(now), postId, parent, text, now, comment => comment with
        {
            Kind = CommentKind.Native,
            AuthorAccountId = authorAccountId,
        });
    }

    /// <summary>
    /// A comment with a given identifier and time, for the demo data whose identifiers come from the seed (ADR-0009).
    /// </summary>
    internal static Comment CreateNative(
        Guid id, Guid authorAccountId, Guid postId, Comment? parent, string text, DateTimeOffset createdAt) =>
        Create(id, postId, parent, text, createdAt, comment => comment with
        {
            Kind = CommentKind.Native,
            AuthorAccountId = authorAccountId,
        });

    /// <summary>
    /// A guest comment carried over from <paramref name="source"/> (FR-TG-6): it needs no account, only the outside
    /// author. It may reply to another guest comment, keeping the nesting of the discussion.
    /// </summary>
    public static Comment CreateGuest(
        Guid postId, Comment? parent, GuestAuthor author, CommentSource source, string text, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(author.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Message);
        return Create(Guid.CreateVersion7(createdAt), postId, parent, text, createdAt, comment => comment with
        {
            Kind = CommentKind.Guest,
            GuestName = author.Name.Trim(),
            GuestUrl = author.Url?.AbsoluteUri,
            SourceChannel = source.Channel,
            SourceMessage = source.Message,
        });
    }

    /// <summary>
    /// The new text of the comment, trimmed (<see cref="CommentRules.CleanText"/>). Whether the author may still change
    /// it (<see cref="CommentRules.ChangeProblem"/>) is checked by the endpoint.
    /// </summary>
    public void Edit(string text, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted comment has no text to change.");
        }

        Text = CommentRules.CleanText(text) ?? throw new ArgumentException("A comment needs its text.", nameof(text));
        EditedAt = now;
    }

    /// <summary>The comment becomes the stub of its replies: its text is gone, its place in the tree stays.</summary>
    public void Delete(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return;
        }

        Text = string.Empty;
        DeletedAt = now;
    }

    private static Comment Create(
        Guid id, Guid postId, Comment? parent, string text, DateTimeOffset now, Func<Fields, Fields> author)
    {
        if (postId == Guid.Empty)
        {
            throw new ArgumentException("A comment needs its post.", nameof(postId));
        }

        if (parent is not null && parent.PostId != postId)
        {
            throw new ArgumentException("A reply is under the post of the comment it replies to.", nameof(parent));
        }

        var clean = CommentRules.CleanText(text)
            ?? throw new ArgumentException("A comment needs its text.", nameof(text));
        var fields = author(new Fields());
        return new Comment
        {
            Id = id,
            PostId = postId,
            ParentId = parent?.Id,
            Path = CommentPath.Of(parent?.Path, id),
            Depth = (parent?.Depth ?? 0) + 1,
            Kind = fields.Kind,
            AuthorAccountId = fields.AuthorAccountId,
            GuestName = fields.GuestName,
            GuestUrl = fields.GuestUrl,
            SourceChannel = fields.SourceChannel,
            SourceMessage = fields.SourceMessage,
            Text = clean,
            CreatedAt = now,
        };
    }

    private sealed record Fields(
        CommentKind Kind = CommentKind.Native,
        Guid? AuthorAccountId = null,
        string? GuestName = null,
        string? GuestUrl = null,
        string? SourceChannel = null,
        string? SourceMessage = null);
}
