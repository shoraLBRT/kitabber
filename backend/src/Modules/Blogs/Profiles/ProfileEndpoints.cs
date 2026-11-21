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
public sealed record Profile(
    Guid Id,
    string Type,
    string? Name,
    string? Nickname,
    string? Description,
    DateTimeOffset? RegisteredAt,
    Guid? AvatarId,
    Uri? AvatarUrl);

/// <summary>What the owner edits in the profile (E04-03). Every field is replaced: missing or empty clears it.</summary>
/// <param name="Name">The name, at most 25 characters.</param>
/// <param name="Description">The description, at most 160 characters.</param>
/// <param name="AvatarId">An image the owner uploaded through <c>POST /api/v1/media</c>; <c>null</c> removes the avatar.</param>
public sealed record ProfileInput(string? Name = null, string? Description = null, Guid? AvatarId = null);

/// <summary>
/// <c>/api/v1/profiles</c> (FR-PROFILE, ADR-0025): a blog's profile by its identifier or by its nickname, open to
/// everyone, and the signed-in account's own profile, which its owner edits (E04-03: name, description, avatar). The
/// nickname rules are E04-05.
/// </summary>
public sealed class ProfileEndpoints : IApiEndpoints
{
    public const string ProfileNotFoundCode = "profile_not_found";

    /// <summary>The side of the delivered avatar: twice the largest avatar on the site (64 px), for dense screens.</summary>
    public const int AvatarSide = 128;

    private static readonly ImageVariant _avatar = ImageVariant.Square(AvatarSide, ImageFormat.WebP);

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

    // Someone else's file is answered as a missing one, so the owner of a file is not given away (ADR-0034).
    private static async Task<string?> AvatarProblemAsync(
        Guid avatarId, Guid accountId, IMediaLibrary media, CancellationToken ct) =>
        await media.FindAsync(avatarId, ct) switch
        {
            null => "Такого загруженного файла нет.",
            { } file when file.OwnerAccountId != accountId => "Такого загруженного файла нет.",
            { Kind: not MediaFileKind.Image } => "Аватар — это изображение.",
            _ => null,
        };

    private static async Task<IResult> GetByIdAsync(
        Guid id, ClaimsPrincipal user, BlogsDbContext db, IMediaLibrary media, CancellationToken ct) =>
        await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) is { } blog
            ? TypedResults.Ok(await ViewAsync(blog, AccountOf(user), media, ct))
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
            ? TypedResults.Ok(await ViewAsync(blog, AccountOf(user), media, ct))
            : ApiProblems.NotFound(ProfileNotFoundCode);
    }

    private static async Task<Profile> ViewAsync(Subsite blog, Guid? viewer, IMediaLibrary media, CancellationToken ct) => new(
        blog.Id,
        blog.Type == SubsiteType.Personal ? "personal" : "topic",
        blog.Name,
        blog.Nickname,
        blog.Description,
        viewer is not null && blog.OwnerAccountId == viewer ? blog.CreatedAt : null,
        blog.AvatarMediaId,
        await AvatarUrlAsync(blog.AvatarMediaId, media, ct));

    // A file that is no longer an image of the media library shows no avatar rather than a broken picture.
    private static async Task<Uri?> AvatarUrlAsync(Guid? avatarId, IMediaLibrary media, CancellationToken ct) =>
        avatarId is { } id && (await media.ImageUrlsAsync([id], _avatar, ct)).TryGetValue(id, out var url) ? url : null;

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
