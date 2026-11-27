using Microsoft.EntityFrameworkCore;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Posts;

/// <summary>A blog on a post's card in a feed (ADR-0052): the author's personal blog or the topic of the post.</summary>
/// <param name="Id">The blog's identifier.</param>
/// <param name="Type"><c>personal</c> or <c>topic</c>.</param>
/// <param name="Name">The blog's name; <c>null</c> until the owner sets it.</param>
/// <param name="Nickname">The short address without <c>@</c>; <c>null</c> until the owner sets it.</param>
/// <param name="AvatarSources">The avatar for every screen density, narrowest first; empty without an avatar.</param>
public sealed record FeedBlogView(
    Guid Id,
    string Type,
    string? Name,
    string? Nickname,
    IReadOnlyList<ImageSource> AvatarSources);

/// <summary>The best comment under a post's card in a feed (E08-15, FR-FEED, ADR-0062).</summary>
/// <param name="Id">The comment's identifier: the card leads to it on the post's page.</param>
/// <param name="Author">
/// The personal blog of the comment's author, with the name and the avatar; <c>null</c> for a guest comment and for an
/// account with no blog yet.
/// </param>
/// <param name="GuestName">The name of a guest comment's outside author (FR-TG-6); <c>null</c> for a native one.</param>
/// <param name="Text">
/// The beginning of the text, at most 300 characters and «…» when it is longer; the card cuts it to a few lines.
/// </param>
/// <param name="AuthorAccountId">
/// The account of a native comment's author; <c>null</c> for a guest one. A reader who blocked it gets the card
/// without the comment (E11-06, ADR-0091).
/// </param>
public sealed record FeedCommentView(
    Guid Id, FeedBlogView? Author, string? GuestName, string Text, Guid? AuthorAccountId = null);

/// <summary>The post a repost carries, as its card shows it inside the repost (E10-08, ADR-0117).</summary>
/// <param name="Post">The original post, as <c>/api/v1/posts/{id}</c> gives it.</param>
/// <param name="Author">The personal blog of the original's author; <c>null</c> when the account has none.</param>
/// <param name="Topic">The topic the original is in; <c>null</c> for a post in a personal blog.</param>
public sealed record FeedOriginalView(PostView Post, FeedBlogView? Author, FeedBlogView? Topic);

/// <summary>A post in a feed with what its card shows around it (E08-02, E08-15, FR-FEED, ADR-0052, ADR-0062).</summary>
/// <param name="Post">The post itself, as <c>/api/v1/posts/{id}</c> gives it.</param>
/// <param name="Author">
/// The personal blog of the post's author; <c>null</c> when the account has none (the blog is made when the account
/// first writes, so this is rare).
/// </param>
/// <param name="Topic">The topic the post is in; <c>null</c> for a post in a personal blog.</param>
/// <param name="BestComment">
/// The most liked shown comment under the post, the earlier one on a tie; <c>null</c> when the post has no comments.
/// </param>
/// <param name="Original">
/// For a repost (<c>post.repostOfPostId</c>), the post it reposts with its author and topic (E10-08, ADR-0117); a repost
/// whose original is deleted is not in a feed at all. <c>null</c> for an ordinary post.
/// </param>
public sealed record FeedPostView(
    PostView Post,
    FeedBlogView? Author,
    FeedBlogView? Topic,
    FeedCommentView? BestComment,
    FeedOriginalView? Original = null);

/// <summary>What the feeds read of the reposts (E10-08, ADR-0117).</summary>
internal static class RepostQueries
{
    /// <summary>
    /// <paramref name="posts"/> without the reposts whose original is no longer shown: deleting a post hides its reposts
    /// (the query filter of <see cref="PostsDbContext"/> leaves the deleted original out of the subquery).
    /// </summary>
    public static IQueryable<Post> WithShownOriginals(this IQueryable<Post> posts, PostsDbContext db) =>
        posts.Where(p => p.RepostOfPostId == null
            || db.Posts.Any(o => o.Id == p.RepostOfPostId && o.Status == PostStatus.Published));
}

/// <summary>
/// Builds the feed views of a page of posts with one read of the best comments and one read of the blogs, the blogs of
/// the comments' authors among them (ADR-0052, ADR-0062): a page costs the same queries whatever its size.
/// </summary>
internal static class FeedPostViews
{
    /// <summary>
    /// The feed views of <paramref name="rows"/>, with the originals of the reposts among them read in one more query
    /// (ADR-0117). A repost whose original is gone by now is left out.
    /// </summary>
    public static async Task<IReadOnlyList<FeedPostView>> BuildAsync(
        IReadOnlyList<Post> rows,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct)
    {
        var originalIds = rows.Select(r => r.RepostOfPostId).OfType<Guid>().Distinct().ToList();
        var originalRows = originalIds.Count == 0
            ? []
            : await db.Posts.AsNoTracking()
                .Where(p => originalIds.Contains(p.Id) && p.Status == PostStatus.Published)
                .ToListAsync(ct);
        var shown = rows.Where(r => r.RepostOfPostId is not { } of || originalRows.Any(o => o.Id == of)).ToList();
        var views = await PostEndpoints.ViewsAsync([.. shown, .. originalRows], media, ct);
        var originals = views.Skip(shown.Count).ToDictionary(v => v.Id);
        return await BuildAsync([.. views.Take(shown.Count)], blogs, comments, ct, originals);
    }

    public static async Task<IReadOnlyList<FeedPostView>> BuildAsync(
        IReadOnlyList<PostView> posts,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct,
        IReadOnlyDictionary<Guid, PostView>? originals = null)
    {
        originals ??= new Dictionary<Guid, PostView>();
        // A repost has no comments of its own on its card: the card leads to the original.
        var best = await comments.BestShownAsync([.. posts.Where(p => p.RepostOfPostId is null).Select(p => p.Id)], ct);
        var cards = await blogs.CardsAsync(
            [.. posts.Select(p => p.BlogId).Concat(originals.Values.Select(o => o.BlogId)).Distinct()],
            [
                .. posts.Select(p => p.AuthorAccountId)
                    .Concat(originals.Values.Select(o => o.AuthorAccountId))
                    .Concat(best.Values.Select(c => c.AuthorAccountId).OfType<Guid>())
                    .Distinct(),
            ],
            ct);
        var byId = cards.ToDictionary(c => c.Id);
        var byOwner = cards
            .Where(c => c.Kind == BlogKind.Personal && c.OwnerAccountId is not null)
            .GroupBy(c => c.OwnerAccountId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        return
        [
            .. posts.Select(post =>
            {
                var blog = byId.GetValueOrDefault(post.BlogId);
                var topic = blog is { Kind: BlogKind.Topic } ? View(blog) : null;
                var author = byOwner.TryGetValue(post.AuthorAccountId, out var own) ? View(own) : null;
                var comment = best.GetValueOrDefault(post.Id) is { } found
                    ? new FeedCommentView(
                        found.Id,
                        found.AuthorAccountId is { } account && byOwner.TryGetValue(account, out var blogOf)
                            ? View(blogOf)
                            : null,
                        found.GuestName,
                        found.Excerpt,
                        found.AuthorAccountId)
                    : null;
                var original = post.RepostOfPostId is { } of && originals.TryGetValue(of, out var reposted)
                    ? new FeedOriginalView(
                        reposted,
                        byOwner.TryGetValue(reposted.AuthorAccountId, out var originalAuthor) ? View(originalAuthor) : null,
                        byId.GetValueOrDefault(reposted.BlogId) is { Kind: BlogKind.Topic } originalTopic
                            ? View(originalTopic)
                            : null)
                    : null;
                return new FeedPostView(post, author, topic, comment, original);
            }),
        ];
    }

    internal static FeedBlogView View(BlogCard card) => new(
        card.Id,
        card.Kind == BlogKind.Personal ? "personal" : "topic",
        card.Name,
        card.Nickname,
        card.AvatarSources);
}
