namespace Platform.Modules.Posts;

/// <summary>Where a post is in its life (FR-POST, ADR-0029).</summary>
public enum PostStatus
{
    /// <summary>Only its author sees it.</summary>
    Draft = 1,

    /// <summary>Everyone sees it.</summary>
    Published = 2,
}

/// <summary>Where a post came from (ADR-0003, FR-TG-5).</summary>
public enum PostOrigin
{
    /// <summary>Written on the platform.</summary>
    Native = 1,

    /// <summary>Carried over from a Telegram channel by the bridge (E19).</summary>
    Telegram = 2,
}

/// <summary>
/// The message a carried-over post came from (ADR-0003): the channel and the message in it are the unique key that
/// makes the import idempotent; the link to the original is optional.
/// </summary>
/// <param name="Channel">The channel in the external service, for example the Telegram channel id.</param>
/// <param name="Message">The message in that channel.</param>
/// <param name="Url">The link to the original, when there is one.</param>
public sealed record PostSource(string Channel, string Message, Uri? Url);

/// <summary>
/// A post (FR-POST, ADR-0029): a document of typed blocks (ADR-0023) by one author in one blog — the author's
/// personal blog or a topic. The author and the blog are known only by their identifiers (FR-ID-1, ADR-0025).
/// Deleting is soft: the row stays with <see cref="DeletedAt"/> set and no one sees it any more.
/// </summary>
public sealed class Post
{
    public Guid Id { get; init; }

    public Guid AuthorAccountId { get; init; }

    /// <summary>The blog (a personal blog or a topic) the post is in.</summary>
    public Guid BlogId { get; private set; }

    /// <summary>The title; <c>null</c> for a short post without one (FR-EDITOR).</summary>
    public string? Title { get; private set; }

    /// <summary>The cleaned block document as JSON, exactly as <c>IBlockDocumentValidator</c> returned it.</summary>
    public string Body { get; private set; } = string.Empty;

    public PostStatus Status { get; private set; }

    public PostOrigin Origin { get; init; } = PostOrigin.Native;

    /// <summary>The external channel of a carried-over post; <c>null</c> for a native one.</summary>
    public string? SourceChannel { get; init; }

    /// <summary>The message in <see cref="SourceChannel"/>; <c>null</c> for a native post.</summary>
    public string? SourceMessage { get; init; }

    /// <summary>The link to the original; may be missing even for a carried-over post.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>
    /// The post this one reposts (E10-08, ADR-0117); <c>null</c> for an ordinary post. A repost is published at once in
    /// its author's personal blog, has no title and no blocks, only <see cref="RepostText"/>, and is not edited after.
    /// </summary>
    public Guid? RepostOfPostId { get; init; }

    /// <summary>What the reposter wrote over the repost; <c>null</c> without a text and for an ordinary post.</summary>
    public string? RepostText { get; init; }

    public bool IsRepost => RepostOfPostId is not null;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>
    /// How many people opened the post, each counted once a day (E07-06, ADR-0071). Only <see cref="ViewFlusher"/>
    /// changes it, in the database, so editing the post never overwrites it; it lags the views by one flush (ADR-0083).
    /// </summary>
    public int ViewCount { get; private set; }

    /// <summary>How many people saw the post's card in a feed, each counted once a day (E07-06, ADR-0071).</summary>
    public int ImpressionCount { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>The source of a carried-over post; <c>null</c> for a native one.</summary>
    public PostSource? Source => SourceChannel is not null && SourceMessage is not null
        ? new PostSource(SourceChannel, SourceMessage, SourceUrl is null ? null : new Uri(SourceUrl))
        : null;

    /// <summary>A new native draft by <paramref name="authorAccountId"/> in <paramref name="blogId"/>.</summary>
    /// <param name="body">The cleaned block document as JSON.</param>
    public static Post CreateDraft(Guid authorAccountId, Guid blogId, string? title, string body, DateTimeOffset now) =>
        Create(Guid.CreateVersion7(now), authorAccountId, blogId, title, body, now, source: null);

    /// <summary>A native draft with a given identifier: for the demo data, whose identifiers come from the seed (ADR-0009).</summary>
    internal static Post CreateDraft(Guid id, Guid authorAccountId, Guid blogId, string? title, string body, DateTimeOffset now) =>
        Create(id, authorAccountId, blogId, title, body, now, source: null);

    /// <summary>A new draft carried over from <paramref name="source"/> (ADR-0003): its origin is Telegram.</summary>
    public static Post CreateImported(
        Guid authorAccountId, Guid blogId, string? title, string body, PostSource source, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Message);
        return Create(Guid.CreateVersion7(now), authorAccountId, blogId, title, body, now, source);
    }

    /// <summary>The body of a repost: a block document without blocks (ADR-0023).</summary>
    public const string EmptyBody = """{"version":1,"blocks":[]}""";

    /// <summary>
    /// A repost of <paramref name="originalPostId"/> by <paramref name="authorAccountId"/> in their personal blog
    /// (E10-08, ADR-0117): published at once, with the text cleaned by <see cref="PostRules.CleanRepostText"/>.
    /// </summary>
    public static Post CreateRepost(
        Guid authorAccountId, Guid blogId, Guid originalPostId, string? text, DateTimeOffset now)
    {
        if (originalPostId == Guid.Empty)
        {
            throw new ArgumentException("A repost needs the post it reposts.", nameof(originalPostId));
        }

        var post = Create(
            Guid.CreateVersion7(now),
            authorAccountId,
            blogId,
            title: null,
            EmptyBody,
            now,
            source: null,
            originalPostId,
            PostRules.CleanRepostText(text));
        post.Publish(now);
        return post;
    }

    /// <summary>Replaces the blog, the title and the body.</summary>
    public void Edit(Guid blogId, string? title, string body, DateTimeOffset now)
    {
        EnsureNotDeleted();
        if (IsRepost)
        {
            throw new InvalidOperationException("A repost is not edited.");
        }

        EnsureBlog(blogId);
        BlogId = blogId;
        Title = PostRules.CleanTitle(title);
        Body = EnsureBody(body);
        UpdatedAt = now;
    }

    /// <summary>
    /// Makes the post visible to everyone; publishing again changes nothing. The checks before publishing (an empty
    /// post, the size limits) are <see cref="PostChecks"/>, run by the endpoint (ADR-0046).
    /// </summary>
    public void Publish(DateTimeOffset now)
    {
        EnsureNotDeleted();
        if (Status == PostStatus.Published)
        {
            return;
        }

        Status = PostStatus.Published;
        PublishedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Deletes the post softly; deleting it again changes nothing.</summary>
    public void Delete(DateTimeOffset now)
    {
        DeletedAt ??= now;
    }

    private static Post Create(
        Guid id,
        Guid authorAccountId,
        Guid blogId,
        string? title,
        string body,
        DateTimeOffset now,
        PostSource? source,
        Guid? repostOf = null,
        string? repostText = null)
    {
        if (authorAccountId == Guid.Empty)
        {
            throw new ArgumentException("A post needs its author's account_id.", nameof(authorAccountId));
        }

        EnsureBlog(blogId);
        return new Post
        {
            Id = id,
            AuthorAccountId = authorAccountId,
            BlogId = blogId,
            Title = PostRules.CleanTitle(title),
            Body = EnsureBody(body),
            Status = PostStatus.Draft,
            Origin = source is null ? PostOrigin.Native : PostOrigin.Telegram,
            SourceChannel = source?.Channel,
            SourceMessage = source?.Message,
            SourceUrl = source?.Url?.AbsoluteUri,
            RepostOfPostId = repostOf,
            RepostText = repostText,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static void EnsureBlog(Guid blogId)
    {
        if (blogId == Guid.Empty)
        {
            throw new ArgumentException("A post needs its blog.", nameof(blogId));
        }
    }

    private static string EnsureBody(string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        return body;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted post cannot change.");
        }
    }
}
