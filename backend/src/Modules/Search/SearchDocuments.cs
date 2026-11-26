using System.Text.Json.Serialization;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Search;

/// <summary>The search indexes (ADR-0094).</summary>
public enum SearchIndexKind
{
    /// <summary>Published posts.</summary>
    Posts,

    /// <summary>Personal blogs and topics, told apart by <see cref="IndexedBlog.Kind"/>.</summary>
    Blogs,
}

/// <summary>A document of a search index: what is stored is its JSON without <see cref="Id"/>, which is the key.</summary>
/// <param name="Id">The post or the blog.</param>
public abstract record IndexedDocument([property: JsonIgnore] Guid Id)
{
    [JsonIgnore]
    public abstract SearchIndexKind Index { get; }

    /// <summary>The text fields a query is matched against, for the fake index of the tests.</summary>
    public abstract IEnumerable<string?> SearchableText();
}

/// <summary>A published post in the index <c>posts</c>.</summary>
public sealed record IndexedPost(
    Guid Id, Guid BlogId, Guid AuthorAccountId, string? Title, string Text, DateTimeOffset PublishedAt)
    : IndexedDocument(Id)
{
    [JsonIgnore]
    public override SearchIndexKind Index => SearchIndexKind.Posts;

    public override IEnumerable<string?> SearchableText() => [Title, Text];

    public static IndexedPost Of(PostSearchDocument post)
    {
        ArgumentNullException.ThrowIfNull(post);
        return new(post.Id, post.BlogId, post.AuthorAccountId, post.Title, post.Text, post.PublishedAt);
    }
}

/// <summary>A personal blog or a topic in the index <c>blogs</c>.</summary>
/// <param name="Kind"><see cref="PersonalKind"/> or <see cref="TopicKind"/>.</param>
public sealed record IndexedBlog(
    Guid Id, string Kind, Guid? OwnerAccountId, string? Name, string? Nickname, string? Description)
    : IndexedDocument(Id)
{
    public const string PersonalKind = "personal";
    public const string TopicKind = "topic";

    [JsonIgnore]
    public override SearchIndexKind Index => SearchIndexKind.Blogs;

    public override IEnumerable<string?> SearchableText() => [Name, Nickname, Description];

    public static IndexedBlog Of(BlogSearchDocument blog)
    {
        ArgumentNullException.ThrowIfNull(blog);
        return new(
            blog.Id,
            blog.Kind == BlogKind.Personal ? PersonalKind : TopicKind,
            blog.OwnerAccountId,
            blog.Name,
            blog.Nickname,
            blog.Description);
    }
}
