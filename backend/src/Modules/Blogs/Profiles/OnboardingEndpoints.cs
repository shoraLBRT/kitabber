using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Messaging;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Blogs.Profiles;

/// <summary>
/// What the person chose in the onboarding (E03-10, ADR-0102). Every step may be skipped: a missing or empty field leaves
/// that part of the blog as it is.
/// </summary>
/// <param name="Name">The name of the blog, 2 to 25 characters; missing or empty — the step was skipped.</param>
/// <param name="AvatarId">An image the person uploaded through <c>POST /api/v1/media</c>; missing — skipped.</param>
/// <param name="TopicIds">The topics to follow; missing or empty — skipped.</param>
public sealed record OnboardingInput(string? Name = null, Guid? AvatarId = null, IReadOnlyList<Guid>? TopicIds = null);

/// <summary>
/// <c>POST /api/v1/profiles/me/onboarding</c> (E03-10, FR-AUTH, ADR-0102): the steps after sign-up — the name, the avatar,
/// the topics — saved at once, and the onboarding marked as done so the site never offers it again. Skipping every step
/// is an empty request that only marks it done.
/// </summary>
public sealed class OnboardingEndpoints : IApiEndpoints
{
    /// <summary>The problem code of a second onboarding: it happens once.</summary>
    public const string OnboardingDoneCode = "blogs.onboarding_done";

    /// <summary>The most topics one onboarding follows: more than the step shows, so a real choice is never cut.</summary>
    public const int MaxTopics = 50;

    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/profiles").WithTags("Profiles")
            .MapPost("/me/onboarding", FinishAsync)
            .RequireAuthorization()
            .Produces<Profile>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("FinishOwnOnboarding")
            .WithSummary("Saves the onboarding's name, avatar and topics of the signed-in account and marks it as done");
    }

    private static async Task<IResult> FinishAsync(
        OnboardingInput input,
        ClaimsPrincipal user,
        PersonalBlogs blogs,
        BlogsDbContext db,
        IModuleOutbox<BlogsDbContext> outbox,
        IMediaLibrary media,
        TimeProvider time,
        CancellationToken ct)
    {
        if (ProfileEndpoints.AccountOf(user) is not { } accountId)
        {
            return TypedResults.Unauthorized();
        }

        var name = SubsiteRules.Clean(input.Name);
        var topicIds = (input.TopicIds ?? []).Distinct().ToList();
        var errors = new Dictionary<string, string[]>();
        if (name is not null && !SubsiteRules.IsValidOnboardingName(name))
        {
            errors["name"] =
                [$"Имя — от {SubsiteRules.OnboardingNameMinLength} до {SubsiteRules.NameMaxLength} символов."];
        }

        if (input.AvatarId is { } avatarId
            && await ProfileEndpoints.AvatarProblemAsync(avatarId, accountId, media, ct) is { } avatarProblem)
        {
            errors["avatarId"] = [avatarProblem];
        }

        if (topicIds.Count > MaxTopics)
        {
            errors["topicIds"] = [$"Не больше {MaxTopics} тем."];
        }
        else if (topicIds.Count > 0
            && await db.Subsites.CountAsync(s => topicIds.Contains(s.Id) && s.Type == SubsiteType.Topic, ct) != topicIds.Count)
        {
            errors["topicIds"] = ["Такой темы нет."];
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var blog = await blogs.EnsureAsync(accountId, ct);
        var now = time.GetUtcNow();
        if (!blog.FinishOnboarding(now))
        {
            return ApiProblems.Conflict(OnboardingDoneCode, "Знакомство с сайтом уже пройдено.");
        }

        if (name is not null)
        {
            blog.Rename(name);
        }

        if (input.AvatarId is not null)
        {
            blog.SetAvatar(input.AvatarId);
        }

        if (name is not null || input.AvatarId is not null)
        {
            // What search finds the blog by may have changed (ADR-0094).
            await outbox.Publish(new BlogChanged(blog.Id, now), ct);
        }

        // The chosen topics become subscriptions (ADR-0085); one already there stays as it is.
        var followed = await db.Subscriptions
            .Where(s => s.AccountId == accountId && topicIds.Contains(s.SubsiteId))
            .Select(s => s.SubsiteId)
            .ToListAsync(ct);
        var owners = await db.Subsites.AsNoTracking()
            .Where(s => topicIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.OwnerAccountId, ct);
        foreach (var topicId in topicIds.Except(followed))
        {
            db.Subscriptions.Add(Subscription.Create(accountId, topicId, now));
            await outbox.Publish(new BlogSubscribed(topicId, BlogKind.Topic, owners[topicId], accountId, now), ct);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ProfileEndpoints.ViewAsync(blog, accountId, db, media, ct));
    }
}
