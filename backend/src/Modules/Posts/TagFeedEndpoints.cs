using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Posts;

/// <summary>
/// <c>/api/v1/feeds/tags/{tag}</c> (E11-08, FR-TOPIC, ADR-0120): the published posts whose text carries a hashtag,
/// newest first, page by page with the cursor of the fresh feed (ADR-0043). The tag is asked for in any case, with or
/// without its <c>#</c>; a word that is not a tag is a validation error. A tag no post carries is an empty feed, not a
/// 404: a link to it is never broken. Pages are cached like the fresh feed's (ADR-0080), the reader's exclusions applied
/// after the cache (ADR-0091).
/// </summary>
public sealed class TagFeedEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/feeds").WithTags("Feeds")
            .MapGet("/tags/{tag}", GetAsync)
            .Produces<CursorPage<FeedPostView>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("GetTagFeed")
            .WithSummary("The published posts with a hashtag, newest first, page by page with a cursor");
    }

    private static async Task<IResult> GetAsync(
        string tag,
        [AsParameters] PageQuery query,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        FeedPageCache cache,
        ClaimsPrincipal user,
        IEnumerable<IFeedExclusionSource> exclusionSources,
        CancellationToken ct)
    {
        if (Hashtags.Parse(tag) is not { } stored)
        {
            return ApiProblems.Validation(
                new Dictionary<string, string[]>
                {
                    ["tag"] = [$"A tag is letters, digits and _ with at least one letter, at most {Hashtags.MaxLength}."],
                },
                "Not a hashtag.");
        }

        var page = query.ToRequest<FreshFeedCursor>();
        var result = await cache.GetOrReadAsync(
            FeedPageCache.TagFeed(stored),
            page,
            token => ReadAsync(stored, page, db, media, blogs, comments, token),
            ct);
        var excluded = await ReaderExclusions.ForAsync(user, exclusionSources, ct);
        return TypedResults.Ok(ReaderExclusions.Apply(result!, excluded));
    }

    private static async Task<CursorPage<FeedPostView>?> ReadAsync(
        string tag,
        PageRequest<FreshFeedCursor> page,
        PostsDbContext db,
        IMediaLibrary media,
        IBlogDirectory blogs,
        ICommentDirectory comments,
        CancellationToken ct)
    {
        string[] wanted = [tag];
        var posts = db.Posts.AsNoTracking()
            .Where(p => p.Status == PostStatus.Published && p.PublishedAt != null && p.Tags.Any(t => wanted.Contains(t)))
            .WithShownOriginals(db);
        if (page.After is { } after)
        {
            posts = posts.Where(p => EF.Functions.LessThan(
                ValueTuple.Create(p.PublishedAt!.Value, p.Id), ValueTuple.Create(after.PublishedAt, after.Id)));
        }

        var rows = await posts
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
            .Take(page.FetchCount)
            .ToListAsync(ct);
        var result = page.ToPage(rows, p => new FreshFeedCursor(p.PublishedAt!.Value, p.Id));
        return new CursorPage<FeedPostView>(
            await FeedPostViews.BuildAsync(result.Items, db, media, blogs, comments, ct), result.NextCursor);
    }
}
