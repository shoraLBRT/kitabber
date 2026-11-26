using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Blogs.Profiles;

/// <summary>The profile of a blog (FR-PROFILE).</summary>
/// <param name="Id">The blog's identifier.</param>
/// <param name="Type"><c>personal</c> or <c>topic</c>.</param>
/// <param name="Name">At most 25 characters; <c>null</c> until the owner sets it.</param>
/// <param name="Nickname">The short address without <c>@</c>; <c>null</c> until the owner sets it.</param>
/// <param name="Description">At most 160 characters.</param>
/// <param name="RegisteredAt">When the owner registered; only the owner sees it.</param>
/// <param name="AvatarId">
/// The avatar: an image uploaded through <c>POST /api/v1/media</c>; <c>null</c> when there is none.
/// </param>
/// <param name="AvatarUrl">
/// The avatar to show: a signed imgproxy link to a WebP square of <see cref="ProfileEndpoints.AvatarSide"/> pixels
/// (ADR-0037); <c>null</c> when there is no avatar.
/// </param>
/// <param name="AvatarSources">
/// The same avatar for every screen density (ADR-0040): WebP squares of <see cref="ProfileEndpoints.AvatarSides"/>
/// pixels, narrowest first, for the <c>srcset</c> of the picture; empty when there is no avatar.
/// </param>
/// <param name="Cover">The cover of the header (E04-04, ADR-0079); <c>null</c> when there is none.</param>
/// <param name="Owner">
/// The owner of a topic (E11-01, ADR-0082): the personal blog of the account that runs it; <c>null</c> for a personal
/// blog (its owner is the blog itself) and for a topic whose owner has no personal blog yet.
/// </param>
public sealed record Profile(
    Guid Id,
    string Type,
    string? Name,
    string? Nickname,
    string? Description,
    DateTimeOffset? RegisteredAt,
    Guid? AvatarId,
    Uri? AvatarUrl,
    IReadOnlyList<ImageSource> AvatarSources,
    ProfileCover? Cover,
    ProfileOwner? Owner = null);

/// <summary>The owner of a topic as its page shows it (ADR-0082): the owner's personal blog.</summary>
/// <param name="Id">The owner's personal blog.</param>
/// <param name="Name">Its name; <c>null</c> until the owner sets it.</param>
/// <param name="Nickname">Its short address without <c>@</c>; <c>null</c> until the owner sets it.</param>
public sealed record ProfileOwner(Guid Id, string? Name, string? Nickname);

/// <summary>The cover of a blog's header (E04-04, ADR-0079): the whole picture and which part of it the header shows.</summary>
/// <param name="Id">The image uploaded through <c>POST /api/v1/media</c>.</param>
/// <param name="Position">
/// The vertical offset of the shown part in percent, 0 (the top edge of the picture) to 100 (the bottom edge), for CSS
/// <c>object-position: 50% {Position}%</c>; the header keeps one proportion on every screen, so it shows the same part.
/// </param>
/// <param name="Sources">
/// The whole picture scaled to the widths of <see cref="ProfileEndpoints.CoverWidths"/> (WebP, ADR-0040), narrowest first.
/// </param>
/// <param name="Preview">The dominant colour and the blurred stand-in until it loads (ADR-0067); <c>null</c> when it has none.</param>
public sealed record ProfileCover(Guid Id, int Position, IReadOnlyList<ImageSource> Sources, ImagePreview? Preview);

/// <summary>A new cover or a new position of the cover (E04-04, ADR-0079).</summary>
/// <param name="MediaId">An image the owner uploaded through <c>POST /api/v1/media</c>.</param>
/// <param name="Position">The vertical offset in percent, 0 to 100; 50 when it is missing.</param>
public sealed record CoverInput(Guid MediaId, int Position = SubsiteRules.CoverPositionDefault);

/// <summary>What the owner edits in the profile (E04-03). Every field is replaced: missing or empty clears it.</summary>
/// <param name="Name">The name, at most 25 characters.</param>
/// <param name="Description">The description, at most 160 characters.</param>
/// <param name="AvatarId">An image the owner uploaded through <c>POST /api/v1/media</c>; <c>null</c> removes the avatar.</param>
public sealed record ProfileInput(string? Name = null, string? Description = null, Guid? AvatarId = null);

/// <summary>
/// <c>/api/v1/profiles</c> (FR-PROFILE, ADR-0025): a blog's profile by its identifier or by its nickname, and the
/// personal blog of an account (the author of a post, ADR-0050), open to everyone, and the signed-in account's own profile, which its owner edits (E04-03: name, description, avatar). The
/// nickname rules are E04-05.
/// </summary>
public sealed class ProfileEndpoints : IApiEndpoints
{
    public const string ProfileNotFoundCode = "profile_not_found";

    /// <summary>The side of the delivered avatar: twice the largest avatar on the site (64 px), for dense screens.</summary>
    public const int AvatarSide = 128;

    /// <summary>
    /// The sides of the avatar sources (ADR-0040): the largest avatar on the site (64 px) at one, two and three device
    /// pixels per CSS pixel.
    /// </summary>
    public static readonly IReadOnlyList<int> AvatarSides = [64, AvatarSide, 192];

    private static readonly ImageVariant _avatar = ImageVariant.Square(AvatarSide, ImageFormat.WebP);

    internal static readonly IReadOnlyList<ImageVariant> AvatarSourceVariants =
        [.. AvatarSides.Select(side => ImageVariant.Square(side, ImageFormat.WebP))];

    /// <summary>
    /// The widths of the cover sources (ADR-0079): the header at a phone's width and at the feed column's width, at one,
    /// two and three device pixels per CSS pixel. The picture is kept whole, so the header picks the part it shows.
    /// </summary>
    public static readonly IReadOnlyList<int> CoverWidths = [400, 640, 800, 1280, 1920];

    internal static readonly IReadOnlyList<ImageVariant> CoverSourceVariants =
        [.. CoverWidths.Select(width => ImageVariant.ToWidth(width, ImageFormat.WebP))];

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var profiles = v1.MapGroup("/profiles").WithTags("Profiles");
        profiles.MapGet("/me", GetOwnAsync)
            .RequireAuthorization()
            .Produces<Profile>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetOwnProfile")
            .WithSummary("The signed-in account's personal blog");
        profiles.MapPut("/me", UpdateOwnAsync)
            .RequireAuthorization()
            .Produces<Profile>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("UpdateOwnProfile")
            .WithSummary("Replaces the name, the description and the avatar of the signed-in account's personal blog");
        profiles.MapPut("/me/cover", SetOwnCoverAsync)
            .RequireAuthorization()
            .Produces<Profile>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("SetOwnCover")
            .WithSummary("Sets the cover of the signed-in account's personal blog, or moves it to a new position");
        profiles.MapDelete("/me/cover", RemoveOwnCoverAsync)
            .RequireAuthorization()
            .Produces<Profile>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("RemoveOwnCover")
            .WithSummary("Removes the cover of the signed-in account's personal blog");
        profiles.MapGet("/{id:guid}", GetByIdAsync)
            .Produces<Profile>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetProfile")
            .WithSummary("A blog's profile by its identifier");
        profiles.MapGet("/by-nickname/{nickname}", GetByNicknameAsync)
            .Produces<Profile>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetProfileByNickname")
            .WithSummary("A blog's profile by its nickname (with or without @, any case)");
        profiles.MapGet("/by-account/{accountId:guid}", GetByAccountAsync)
            .Produces<Profile>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetProfileByAccount")
            .WithSummary("The personal blog of an account, such as the author of a post (ADR-0050)");
    }

    private static async Task<IResult> GetOwnAsync(
        ClaimsPrincipal user, PersonalBlogs blogs, IMediaLibrary media, CancellationToken ct)
    {
        if (AccountOf(user) is not { } accountId)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(await ViewAsync(await blogs.EnsureAsync(accountId, ct), accountId, media, ct));
    }

    private static async Task<IResult> UpdateOwnAsync(
        ProfileInput input,
        ClaimsPrincipal user,
        PersonalBlogs blogs,
        BlogsDbContext db,
        IMediaLibrary media,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } accountId)
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>();
        if (!SubsiteRules.IsValidName(input.Name))
        {
            errors["name"] = [$"Название — не длиннее {SubsiteRules.NameMaxLength} символов."];
        }

        if (!SubsiteRules.IsValidDescription(input.Description))
        {
            errors["description"] = [$"Описание — не длиннее {SubsiteRules.DescriptionMaxLength} символов."];
        }

        if (input.AvatarId is { } avatarId && await AvatarProblemAsync(avatarId, accountId, media, ct) is { } avatarProblem)
        {
            errors["avatarId"] = [avatarProblem];
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var blog = await blogs.EnsureAsync(accountId, ct);
        blog.Rename(input.Name);
        blog.Describe(input.Description);
        blog.SetAvatar(input.AvatarId);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(blog, accountId, media, ct));
    }

    private static async Task<IResult> SetOwnCoverAsync(
        CoverInput input,
        ClaimsPrincipal user,
        PersonalBlogs blogs,
        BlogsDbContext db,
        IMediaLibrary media,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } accountId)
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>();
        if (await ImageProblemAsync(input.MediaId, accountId, "Обложка — это изображение.", media, ct) is { } coverProblem)
        {
            errors["mediaId"] = [coverProblem];
        }

        if (!SubsiteRules.IsValidCoverPosition(input.Position))
        {
            errors["position"] = [$"Положение обложки — от 0 до {SubsiteRules.CoverPositionMax}."];
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var blog = await blogs.EnsureAsync(accountId, ct);
        blog.SetCover(input.MediaId, input.Position);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(blog, accountId, media, ct));
    }

    private static async Task<IResult> RemoveOwnCoverAsync(
        ClaimsPrincipal user, PersonalBlogs blogs, BlogsDbContext db, IMediaLibrary media, CancellationToken ct)
    {
        if (AccountOf(user) is not { } accountId)
        {
            return TypedResults.Unauthorized();
        }

        var blog = await blogs.EnsureAsync(accountId, ct);
        blog.RemoveCover();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(blog, accountId, media, ct));
    }

    private static Task<string?> AvatarProblemAsync(Guid avatarId, Guid accountId, IMediaLibrary media, CancellationToken ct) =>
        ImageProblemAsync(avatarId, accountId, "Аватар — это изображение.", media, ct);

    // Someone else's file is answered as a missing one, so the owner of a file is not given away (ADR-0034).
    private static async Task<string?> ImageProblemAsync(
        Guid mediaId, Guid accountId, string notImage, IMediaLibrary media, CancellationToken ct) =>
        await media.FindAsync(mediaId, ct) switch
        {
            null => "Такого загруженного файла нет.",
            { } file when file.OwnerAccountId != accountId => "Такого загруженного файла нет.",
            { Kind: not MediaFileKind.Image } => notImage,
            _ => null,
        };

    private static async Task<IResult> GetByIdAsync(
        Guid id, ClaimsPrincipal user, BlogsDbContext db, IMediaLibrary media, CancellationToken ct) =>
        await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) is { } blog
            ? TypedResults.Ok(await ReadAsync(blog, AccountOf(user), db, media, ct))
            : ApiProblems.NotFound(ProfileNotFoundCode);

    // Only finds the blog: a reader's request never creates a personal blog for someone else's account.
    private static async Task<IResult> GetByAccountAsync(
        Guid accountId, ClaimsPrincipal user, BlogsDbContext db, IMediaLibrary media, CancellationToken ct) =>
        await db.Subsites.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Type == SubsiteType.Personal && s.OwnerAccountId == accountId, ct) is { } blog
            ? TypedResults.Ok(await ReadAsync(blog, AccountOf(user), db, media, ct))
            : ApiProblems.NotFound(ProfileNotFoundCode);

    private static async Task<IResult> GetByNicknameAsync(
        string nickname, ClaimsPrincipal user, BlogsDbContext db, IMediaLibrary media, CancellationToken ct)
    {
        if (SubsiteRules.NormalizeNickname(nickname) is not { } normalized)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["nickname"] = ["Никнейм — от 3 до 32 латинских букв, цифр и _, начинается с буквы."],
            });
        }

        return await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Nickname == normalized, ct) is { } blog
            ? TypedResults.Ok(await ReadAsync(blog, AccountOf(user), db, media, ct))
            : ApiProblems.NotFound(ProfileNotFoundCode);
    }

    // A reader's view of any blog: a topic also names its owner (ADR-0082).
    private static async Task<Profile> ReadAsync(
        Subsite blog, Guid? viewer, BlogsDbContext db, IMediaLibrary media, CancellationToken ct) =>
        await ViewAsync(blog, viewer, media, ct) with { Owner = await TopicOwnerAsync(blog, db, ct) };

    // The owner of a topic is shown by the owner's personal blog; a reader's request never creates that blog.
    private static async Task<ProfileOwner?> TopicOwnerAsync(Subsite blog, BlogsDbContext db, CancellationToken ct) =>
        blog is { Type: SubsiteType.Topic, OwnerAccountId: { } owner }
            ? await db.Subsites.AsNoTracking()
                .Where(s => s.Type == SubsiteType.Personal && s.OwnerAccountId == owner)
                .Select(s => new ProfileOwner(s.Id, s.Name, s.Nickname))
                .SingleOrDefaultAsync(ct)
            : null;

    private static async Task<Profile> ViewAsync(Subsite blog, Guid? viewer, IMediaLibrary media, CancellationToken ct) => new(
        blog.Id,
        blog.Type == SubsiteType.Personal ? "personal" : "topic",
        blog.Name,
        blog.Nickname,
        blog.Description,
        viewer is not null && blog.OwnerAccountId == viewer ? blog.CreatedAt : null,
        blog.AvatarMediaId,
        await AvatarUrlAsync(blog.AvatarMediaId, media, ct),
        await AvatarSourcesAsync(blog.AvatarMediaId, media, ct),
        await CoverAsync(blog, media, ct));

    // A file that is no longer an image of the media library shows no cover rather than a broken picture.
    private static async Task<ProfileCover?> CoverAsync(Subsite blog, IMediaLibrary media, CancellationToken ct)
    {
        if (blog.CoverMediaId is not { } id
            || !(await media.ImageSourcesAsync([id], CoverSourceVariants, ct)).TryGetValue(id, out var sources))
        {
            return null;
        }

        var previews = await media.ImagePreviewsAsync([id], ct);
        return new ProfileCover(id, blog.CoverPosition, sources, previews.GetValueOrDefault(id));
    }

    // A file that is no longer an image of the media library shows no avatar rather than a broken picture.
    private static async Task<Uri?> AvatarUrlAsync(Guid? avatarId, IMediaLibrary media, CancellationToken ct) =>
        avatarId is { } id && (await media.ImageUrlsAsync([id], _avatar, ct)).TryGetValue(id, out var url) ? url : null;

    private static async Task<IReadOnlyList<ImageSource>> AvatarSourcesAsync(
        Guid? avatarId, IMediaLibrary media, CancellationToken ct) =>
        avatarId is { } id && (await media.ImageSourcesAsync([id], AvatarSourceVariants, ct)).TryGetValue(id, out var sources)
            ? sources
            : [];

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
