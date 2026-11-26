using MassTransit;
using Platform.Messaging;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Search;

/// <summary>
/// Brings a post's document in line with the post (ADR-0094): a published post is written, a deleted one or a draft is
/// removed. The event says only which post changed; its state is read now, so a late or repeated event does no harm.
/// </summary>
public sealed class PostChangedSearchConsumer(IPostSearchSource posts, ISearchIndex index) : IConsumer<PostChanged>
{
    public async Task Consume(ConsumeContext<PostChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var id = context.Message.PostId;
        if (await posts.FindAsync(id, context.CancellationToken) is { } post)
        {
            await index.PutAsync(IndexedPost.Of(post), context.CancellationToken);
        }
        else
        {
            await index.RemoveAsync(SearchIndexKind.Posts, id, context.CancellationToken);
        }
    }
}

/// <summary>The inbox of the Search schema for <see cref="PostChangedSearchConsumer"/> (ADR-0005).</summary>
public sealed class PostChangedSearchConsumerDefinition
    : ModuleConsumerDefinition<PostChangedSearchConsumer, SearchDbContext>;

/// <summary>Brings the document of a personal blog or a topic in line with it (ADR-0094), as for posts.</summary>
public sealed class BlogChangedSearchConsumer(IBlogSearchSource blogs, ISearchIndex index) : IConsumer<BlogChanged>
{
    public async Task Consume(ConsumeContext<BlogChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var id = context.Message.BlogId;
        if (await blogs.FindAsync(id, context.CancellationToken) is { } blog)
        {
            await index.PutAsync(IndexedBlog.Of(blog), context.CancellationToken);
        }
        else
        {
            await index.RemoveAsync(SearchIndexKind.Blogs, id, context.CancellationToken);
        }
    }
}

/// <summary>The inbox of the Search schema for <see cref="BlogChangedSearchConsumer"/> (ADR-0005).</summary>
public sealed class BlogChangedSearchConsumerDefinition
    : ModuleConsumerDefinition<BlogChangedSearchConsumer, SearchDbContext>;
