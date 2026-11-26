using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Editor.Contracts;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Posts;

/// <summary>What a post is made of, when it is created or edited.</summary>
/// <param name="BlogId">
/// The blog: the author's personal blog or a topic. On creation, missing means the author's personal blog; on editing,
/// missing keeps the post where it is.
/// </param>
/// <param name="Title">The title; empty or missing for a short post without one.</param>
/// <param name="Body">The block document <c>{"version", "blocks": [{"id", "type", "data"}]}</c> (ADR-0023).</param>
public sealed record PostInput(JsonElement Body, Guid? BlogId = null, string? Title = null);

/// <summary>The external source of a post carried over from Telegram (ADR-0003).</summary>
/// <param name="Channel">The channel in the external service.</param>
/// <param name="Message">The message in that channel.</param>
/// <param name="Url">The link to the original, when there is one.</param>
public sealed record PostSourceView(string Channel, string Message, string? Url);

/// <summary>A post (FR-POST).</summary>
/// <param name="Id">The post's identifier.</param>
/// <param name="BlogId">The blog it is in.</param>
/// <param name="AuthorAccountId">Its author.</param>
/// <param name="Title"><c>null</c> for a short post.</param>
/// <param name="Slug">
/// The slug of the post's address <c>&lt;blog address&gt;/&lt;id&gt;-&lt;slug&gt;</c>, transliterated from the title
/// (ADR-0041); <c>null</c> for a post without a title, whose address ends with the id alone.
/// </param>
/// <param name="Body">The cleaned block document.</param>
/// <param name="Status"><c>draft</c> (only the author sees it) or <c>published</c>.</param>
/// <param name="Origin"><c>native</c> or <c>telegram</c>.</param>
/// <param name="Source">Where a carried-over post came from; <c>null</c> for a native post.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="PublishedAt">When it was published; <c>null</c> for a draft.</param>
/// <param name="Images">
/// The pictures of the image blocks (ADR-0049), keyed by the file id the block names: signed sources of a responsive
/// picture (ADR-0040), widest last. A file that is no longer an uploaded image has no entry.
/// </param>
/// <param name="ImagePreviews">
/// What each picture of <paramref name="Images"/> is shown as until it loads (ADR-0067): the dominant colour and a
/// tiny blurred stand-in, by the same key. A picture without a preview has no entry.
/// </param>
/// <param name="ViewCount">
/// How many people opened the post, each counted once a day; bots and the author are not counted (E07-06, ADR-0071).
/// </param>
public sealed record PostView(
    Guid Id,
    Guid BlogId,
    Guid AuthorAccountId,
    string? Title,
    string? Slug,
    JsonObject Body,
    string Status,
    string Origin,
    PostSourceView? Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyDictionary<Guid, IReadOnlyList<ImageSource>> Images,
    IReadOnlyDictionary<Guid, ImagePreview> ImagePreviews,
    int ViewCount);

/// <summary>
/// <c>/api/v1/posts</c> (FR-POST, ADR-0029): create a draft, read, edit and softly delete a post. A draft is seen only
/// by its author; only the author edits, publishes and deletes. Publishing needs a confirmed email and passes the
/// checks of <see cref="PostChecks"/> (E05-09, ADR-0046).
/// </summary>
public sealed class PostEndpoints : IApiEndpoints
{
    public const string PostNotFoundCode = "posts.post_not_found";
    public const string NotAuthorCode = "posts.not_author";
    public const string BlogForbiddenCode = "posts.blog_forbidden";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var posts = v1.MapGroup("/posts").WithTags("Posts");
        posts.MapPost("/", CreateAsync)
            .RequireAuthorization()
            .Produces<PostView>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("CreatePost")
            .WithSummary("Creates a draft in the author's personal blog or in a topic");
        posts.MapGet("/{id:guid}", GetAsync)
            .Produces<PostView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetPost")
            .WithSummary("A published post, or the signed-in author's own draft");
        posts.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization()
            .Produces<PostView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("UpdatePost")
            .WithSummary("Replaces the blog, the title and the body of the author's post");
        posts.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DeletePost")
            .WithSummary("Deletes the author's post softly");
        posts.MapPost("/{id:guid}/publish", PublishAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<PostView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("PublishPost")
            .WithSummary("Publishes the author's draft once it passes the checks; a published post stays as it is");
    }

    private static async Task<IResult> CreateAsync(
        PostInput input,
        ClaimsPrincipal user,
        PostsDbContext db,
        IBlogDirectory blogs,
        IBlockDocumentValidator validator,
        IMediaLibrary media,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if ((CleanBody(input.Body, validator, out var body) ?? await ImageProblemAsync(body, account, media, ct))
            is { } invalid)
        {
            return invalid;
        }

        var blog = input.BlogId is { } blogId
            ? await blogs.FindAsync(blogId, ct)
            : await blogs.EnsurePersonalBlogAsync(account, ct);
        if (BlogProblem(blog, account) is { } problem)
        {
            return problem;
        }

        var post = Post.CreateDraft(account, blog!.Id, input.Title, body, time.GetUtcNow());
        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/posts/{post.Id}", await ViewAsync(post, media, ct));
    }

    private static async Task<IResult> GetAsync(
        Guid id, ClaimsPrincipal user, PostsDbContext db, IMediaLibrary media, CancellationToken ct) =>
        await db.Posts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) is { } post
        && PostRules.MaySee(post, AccountOf(user))
            ? TypedResults.Ok(await ViewAsync(post, media, ct))
            : ApiProblems.NotFound(PostNotFoundCode);

    private static async Task<IResult> UpdateAsync(
        Guid id,
        PostInput input,
        ClaimsPrincipal user,
        PostsDbContext db,
        IBlogDirectory blogs,
        IBlockDocumentValidator validator,
        IMediaLibrary media,
        FeedPageCache feed,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var post = await db.Posts.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (ChangeProblem(post, account) is { } refused)
        {
            return refused;
        }

        if ((CleanBody(input.Body, validator, out var body) ?? await ImageProblemAsync(body, account, media, ct))
            is { } invalid)
        {
            return invalid;
        }

        if (input.BlogId is { } blogId && blogId != post!.BlogId
            && BlogProblem(await blogs.FindAsync(blogId, ct), account) is { } problem)
        {
            return problem;
        }

        // A published post stays publishable: an edit is held to the same checks as publishing.
        if (post!.Status == PostStatus.Published
            && PostChecks.Problems(PostRules.CleanTitle(input.Title), body, validator) is { Count: > 0 } unpublishable)
        {
            return ApiProblems.Validation(unpublishable, "The post does not pass the checks of publishing.");
        }

        post.Edit(input.BlogId ?? post.BlogId, input.Title, body, time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        if (post.Status == PostStatus.Published)
        {
            await feed.InvalidateAsync(ct);
        }

        return TypedResults.Ok(await ViewAsync(post, media, ct));
    }

    private static async Task<IResult> PublishAsync(
        Guid id,
        ClaimsPrincipal user,
        PostsDbContext db,
        IBlockDocumentValidator validator,
        IMediaLibrary media,
        FeedPageCache feed,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var post = await db.Posts.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (ChangeProblem(post, account) is { } refused)
        {
            return refused;
        }

        if (post!.Status == PostStatus.Published)
        {
            return TypedResults.Ok(await ViewAsync(post, media, ct));
        }

        if (PostChecks.Problems(post.Title, post.Body, validator) is { Count: > 0 } problems)
        {
            return ApiProblems.Validation(problems, "The post does not pass the checks of publishing.");
        }

        post.Publish(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await feed.InvalidateAsync(ct);
        return TypedResults.Ok(await ViewAsync(post, media, ct));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, ClaimsPrincipal user, PostsDbContext db, FeedPageCache feed, TimeProvider time, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var post = await db.Posts.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (ChangeProblem(post, account) is { } refused)
        {
            return refused;
        }

        var shownInFeed = post!.Status == PostStatus.Published && post.DeletedAt is null;
        post.Delete(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        if (shownInFeed)
        {
            await feed.InvalidateAsync(ct);
        }

        return TypedResults.NoContent();
    }

    // A post no one but its author may see is not found for anyone else; a published post of another author is
    // refused.
    private static IResult? ChangeProblem(Post? post, Guid account) =>
        post is null || !PostRules.MaySee(post, account) ? ApiProblems.NotFound(PostNotFoundCode)
        : !PostRules.MayChange(post, account) ? ApiProblems.Forbidden(NotAuthorCode, "Only the author changes a post.")
        : null;

    private static IResult? BlogProblem(BlogSummary? blog, Guid account) =>
        blog is null
            ? ApiProblems.Validation(new Dictionary<string, string[]> { ["blogId"] = ["Такого блога нет."] })
            : !PostRules.MayPostIn(blog, account)
                ? ApiProblems.Forbidden(BlogForbiddenCode, "Only the owner posts in a personal blog.")
                : null;

    // Puts the cleaned document as JSON into body; a validation problem when the document is missing or does not match
    // the block schemas.
    private static IResult? CleanBody(JsonElement input, IBlockDocumentValidator validator, out string body)
    {
        body = string.Empty;
        if (input.ValueKind != JsonValueKind.Object)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["body"] = ["Нужен документ из блоков."] });
        }

        var result = validator.Validate(input);
        if (result.IsValid)
        {
            body = result.Document!.ToJsonString();
            return null;
        }

        var errors = result.Errors
            .GroupBy(e => e.Path.Length == 0 ? "body" : $"body.{e.Path}", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal);
        return ApiProblems.Validation(errors, "The body does not match the block schemas.");
    }

    // A picture only its author uploaded may be in a post (ADR-0049).
    private static async Task<IResult?> ImageProblemAsync(
        string body, Guid account, IMediaLibrary media, CancellationToken ct) =>
        await PostImages.ProblemsAsync(JsonNode.Parse(body)!.AsObject(), account, media, ct) is { Count: > 0 } problems
            ? ApiProblems.Validation(problems, "The body names pictures the author did not upload.")
            : null;

    private static async Task<PostView> ViewAsync(Post post, IMediaLibrary media, CancellationToken ct) =>
        (await ViewsAsync([post], media, ct))[0];

    /// <summary>The views of <paramref name="posts"/>, with the sources of all their pictures fetched at once.</summary>
    internal static async Task<IReadOnlyList<PostView>> ViewsAsync(
        IReadOnlyList<Post> posts, IMediaLibrary media, CancellationToken ct)
    {
        var bodies = posts.Select(p => JsonNode.Parse(p.Body)!.AsObject()).ToList();
        var sources = await PostImages.SourcesAsync(bodies, media, ct);
        var previews = await PostImages.PreviewsAsync(bodies, media, ct);
        return [.. posts.Select((post, i) => View(post, bodies[i], sources, previews))];
    }

    private static PostView View(
        Post post,
        JsonObject body,
        IReadOnlyDictionary<Guid, IReadOnlyList<ImageSource>> sources,
        IReadOnlyDictionary<Guid, ImagePreview> previews) => new(
        post.Id,
        post.BlogId,
        post.AuthorAccountId,
        post.Title,
        PostSlug.From(post.Title),
        body,
        post.Status == PostStatus.Published ? "published" : "draft",
        post.Origin == PostOrigin.Telegram ? "telegram" : "native",
        post.Source is { } source ? new PostSourceView(source.Channel, source.Message, source.Url?.AbsoluteUri) : null,
        post.CreatedAt,
        post.UpdatedAt,
        post.PublishedAt,
        PostImages.MediaIds(body)
            .Select(m => m.MediaId)
            .Distinct()
            .Where(sources.ContainsKey)
            .ToDictionary(id => id, id => sources[id]),
        PostImages.MediaIds(body)
            .Select(m => m.MediaId)
            .Distinct()
            .Where(previews.ContainsKey)
            .ToDictionary(id => id, id => previews[id]),
        post.ViewCount);

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    internal static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
