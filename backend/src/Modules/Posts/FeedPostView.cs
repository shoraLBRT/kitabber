using Platform.Modules.Blogs.Contracts;
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

/// <summary>A post in a feed with what its card shows around it (E08-02, FR-FEED, ADR-0052).</summary>
/// <param name="Post">The post itself, as <c>/api/v1/posts/{id}</c> gives it.</param>
/// <param name="Author">
/// The personal blog of the post's author; <c>null</c> when the account has none (the blog is made when the account
/// first writes, so this is rare).
/// </param>
/// <param name="Topic">The topic the post is in; <c>null</c> for a post in a personal blog.</param>
public sealed record FeedPostView(PostView Post, FeedBlogView? Author, FeedBlogView? Topic);

/// <summary>Builds the feed views of a page of posts with one read of the blogs (ADR-0052).</summary>
internal static class FeedPostViews
{
    public static async Task<IReadOnlyList<FeedPostView>> BuildAsync(
        IReadOnlyList<PostView> posts, IBlogDirectory blogs, CancellationToken ct)
    {
        var cards = await blogs.CardsAsync(
            [.. posts.Select(p => p.BlogId).Distinct()],
            [.. posts.Select(p => p.AuthorAccountId).Distinct()],
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
                return new FeedPostView(post, author, topic);
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
