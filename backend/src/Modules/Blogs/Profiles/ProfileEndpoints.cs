using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;

namespace Platform.Modules.Blogs.Profiles;

/// <summary>The profile of a blog (FR-PROFILE).</summary>
/// <param name="Id">The blog's identifier.</param>
/// <param name="Type"><c>personal</c> or <c>topic</c>.</param>
/// <param name="Name">At most 25 characters; <c>null</c> until the owner sets it.</param>
/// <param name="Nickname">The short address without <c>@</c>; <c>null</c> until the owner sets it.</param>
/// <param name="Description">At most 160 characters.</param>
/// <param name="RegisteredAt">When the owner registered; only the owner sees it.</param>
public sealed record Profile(Guid Id, string Type, string? Name, string? Nickname, string? Description, DateTimeOffset? RegisteredAt);

/// <summary>
/// <c>/api/v1/profiles</c> (FR-PROFILE, ADR-0025): a blog's profile by its identifier or by its nickname, open to
/// everyone, and the signed-in account's own profile. Editing the profile is E04-03, the nickname rules E04-05.
/// </summary>
public sealed class ProfileEndpoints : IApiEndpoints
{
    public const string ProfileNotFoundCode = "profile_not_found";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var profiles = v1.MapGroup("/profiles").WithTags("Profiles");
        profiles.MapGet("/me", GetOwnAsync)
            .RequireAuthorization()
            .Produces<Profile>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetOwnProfile")
            .WithSummary("The signed-in account's personal blog");
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

    private static async Task<IResult> GetOwnAsync(ClaimsPrincipal user, PersonalBlogs blogs, CancellationToken ct)
    {
        if (AccountOf(user) is not { } accountId)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(View(await blogs.EnsureAsync(accountId, ct), accountId));
    }

    private static async Task<IResult> GetByIdAsync(Guid id, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct) =>
        await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) is { } blog
            ? TypedResults.Ok(View(blog, AccountOf(user)))
            : ApiProblems.NotFound(ProfileNotFoundCode);

    private static async Task<IResult> GetByNicknameAsync(
        string nickname, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct)
    {
        if (SubsiteRules.NormalizeNickname(nickname) is not { } normalized)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["nickname"] = ["Никнейм — от 3 до 32 латинских букв, цифр и _, начинается с буквы."],
            });
        }

        return await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Nickname == normalized, ct) is { } blog
            ? TypedResults.Ok(View(blog, AccountOf(user)))
            : ApiProblems.NotFound(ProfileNotFoundCode);
    }

    private static Profile View(Subsite blog, Guid? viewer) => new(
        blog.Id,
        blog.Type == SubsiteType.Personal ? "personal" : "topic",
        blog.Name,
        blog.Nickname,
        blog.Description,
        viewer is not null && blog.OwnerAccountId == viewer ? blog.CreatedAt : null);

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
