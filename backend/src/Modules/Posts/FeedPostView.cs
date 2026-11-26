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
public sealed record FeedCommentView(Guid Id, FeedBlogView? Author, string? GuestName, string Text);

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
public sealed record FeedPostView(
    PostView Post, FeedBlogView? Author, FeedBlogView? Topic, FeedCommentView? BestComment);

/// <summary>
/// Builds the feed views of a page of posts with one read of the best comments and one read of the blogs, the blogs of
/// the comments' authors among them (ADR-0052, ADR-0062): a page costs the same queries whatever its size.
/// </summary>
internal static class FeedPostViews
{
    public static async Task<IReadOnlyList<FeedPostView>> BuildAsync(
        IReadOnlyList<PostView> posts, IBlogDirectory blogs, ICommentDirectory comments, CancellationToken ct)
    {
        var best = await comments.BestShownAsync([.. posts.Select(p => p.Id)], ct);
        var cards = await blogs.CardsAsync(
            [.. posts.Select(p => p.BlogId).Distinct()],
            [
                .. posts.Select(p => p.AuthorAccountId)
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
                        found.Excerpt)
                    : null;
                return new FeedPostView(post, author, topic, comment);
            }),
        ];
    }

    private static FeedBlogView View(BlogCard card) => new(
        card.Id,
        card.Kind == BlogKind.Personal ? "personal" : "topic",
        card.Name,
        card.Nickname,
        card.AvatarSources);
}
