using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Messaging;
using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Blogs.Topics;

/// <summary>A topic as the editor offers it to choose from (ADR-0046).</summary>
/// <param name="Id">The topic's identifier: the <c>blogId</c> of a post in it.</param>
/// <param name="Name">Its name.</param>
/// <param name="Nickname">Its address: <c>/&lt;nickname&gt;</c>.</param>
public sealed record TopicSummary(Guid Id, string Name, string Nickname);

/// <summary>A new topic (E11-11, ADR-0084).</summary>
/// <param name="Name">Its name: from 2 to 25 characters, unique among topics in any case.</param>
/// <param name="Nickname">Its address <c>/&lt;nickname&gt;</c>: the nickname rules, free among blogs and topics, not reserved.</param>
/// <param name="Description">What it is about, at most 160 characters; empty or <c>null</c> for none.</param>
public sealed record TopicInput(string? Name, string? Nickname, string? Description);

/// <summary>
/// <c>/api/v1/topics</c> (FR-EDITOR, ADR-0046): every topic with a name and an address, by name, open to everyone —
/// the list the editor's topic choice offers; and a new topic by a person (E11-11, ADR-0084). The catalogue with search
/// and the menu's list are <see cref="TopicCatalogEndpoints"/> (E11-02, ADR-0088).
/// </summary>
public sealed class TopicEndpoints : IApiEndpoints
{
    /// <summary>At most this many topics are listed; the catalogue with search and paging is <see cref="TopicCatalogEndpoints"/>.</summary>
    public const int MaxTopics = 500;

    /// <summary>The problem code of a new topic over <see cref="TopicLimits.MaxPerAccount"/>.</summary>
    public const string TopicLimitReachedCode = "blogs.topic_limit_reached";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/topics").WithTags("Topics")
            .MapGet("/", ListAsync)
            .Produces<TopicSummary[]>()
            .WithName("ListTopics")
            .WithSummary("Every topic a post may be published in, by name");
        v1.MapGroup("/topics").WithTags("Topics")
            .MapPost("/", CreateAsync)
            .RequireAuthorization(AccountPolicies.ConfirmedEmail)
            .Produces<TopicSummary>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("CreateTopic")
            .WithSummary("Creates a topic owned by the signed-in account (a confirmed email, a limit of topics per account)");
    }

    private static async Task<IResult> CreateAsync(
        TopicInput input,
        ClaimsPrincipal user,
        BlogsDbContext db,
        IModuleOutbox<BlogsDbContext> outbox,
        TopicLimits limits,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } accountId)
        {
            return TypedResults.Unauthorized();
        }

        var owned = await db.Subsites.CountAsync(s => s.Type == SubsiteType.Topic && s.OwnerAccountId == accountId, ct);
        if (owned >= limits.MaxPerAccount)
        {
            return ApiProblems.Conflict(
                TopicLimitReachedCode, $"Одному аккаунту можно создать не больше {limits.MaxPerAccount} тем.");
        }

        var name = SubsiteRules.Clean(input.Name);
        var nickname = SubsiteRules.NormalizeNickname(input.Nickname);
        var errors = new Dictionary<string, string[]>();
        if (NameProblem(name, limits) is { } nameProblem)
        {
            errors["name"] = [nameProblem];
        }

        if (NicknameProblem(input.Nickname, nickname, limits) is { } nicknameProblem)
        {
            errors["nickname"] = [nicknameProblem];
        }

        if (!SubsiteRules.IsValidDescription(input.Description))
        {
            errors["description"] = [$"Описание — не длиннее {SubsiteRules.DescriptionMaxLength} символов."];
        }

        if (!errors.ContainsKey("name") && await NameTakenAsync(db, name!, ct))
        {
            errors["name"] = ["Тема с таким названием уже есть."];
        }

        var now = time.GetUtcNow();
        if (!errors.ContainsKey("nickname") && await BlogAddresses.IsTakenAsync(db, nickname!, null, now, ct))
        {
            errors["nickname"] = [_addressTaken];
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var topic = Subsite.CreateTopic(name!, nickname!, accountId, now);
        topic.Describe(input.Description);
        // A nickname a blog gave up long ago stops leading to that blog (ADR-0090).
        await BlogAddresses.ClaimAsync(db, nickname!, ct);
        db.Subsites.Add(topic);
        // The new topic and its event are one change (ADR-0094): search finds it from then on.
        await outbox.Publish(new BlogChanged(topic.Id, now), ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (BlogAddresses.IsAddressRace(e))
        {
            // Someone took the address between the check and the save.
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["nickname"] = [_addressTaken] });
        }

        return TypedResults.Created(
            $"/api/v1/profiles/{topic.Id}", new TopicSummary(topic.Id, topic.Name!, topic.Nickname!));
    }

    private const string _addressTaken = "Этот адрес уже занят блогом или темой.";

    private static string? NameProblem(string? name, TopicLimits limits)
    {
        if (!TopicCreationRules.IsLongEnoughName(name))
        {
            return $"Название — от {TopicCreationRules.NameMinLength} до {SubsiteRules.NameMaxLength} символов.";
        }

        if (!SubsiteRules.IsValidName(name))
        {
            return $"Название — не длиннее {SubsiteRules.NameMaxLength} символов.";
        }

        return TopicCreationRules.StopWordIn(name, limits.StopWords) is not null
            ? "Такое название темы выбрать нельзя."
            : null;
    }

    private static string? NicknameProblem(string? input, string? nickname, TopicLimits limits)
    {
        if (nickname is null)
        {
            return string.IsNullOrWhiteSpace(input)
                ? "Укажите адрес темы."
                : "Адрес — от 3 до 32 латинских букв, цифр и _, начинается с буквы.";
        }

        return BlogAddresses.IsRefused(nickname, limits) ? "Такой адрес выбрать нельзя." : null;
    }

    // The same name in another case is a duplicate. Without a unique index two topics with one name could still be made
    // at the same moment (ADR-0084): a rare case the moderation renames.
    private static Task<bool> NameTakenAsync(BlogsDbContext db, string name, CancellationToken ct)
    {
        var pattern = TopicCatalogRules.EscapeLike(name);
        return db.Subsites.AnyAsync(
            s => s.Type == SubsiteType.Topic && s.Name != null && EF.Functions.ILike(s.Name, pattern, @"\"), ct);
    }

    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;

    private static async Task<IResult> ListAsync(BlogsDbContext db, CancellationToken ct)
    {
        var topics = await db.Subsites.AsNoTracking()
            .Where(s => s.Type == SubsiteType.Topic && s.Name != null && s.Nickname != null)
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Take(MaxTopics)
            .Select(s => new TopicSummary(s.Id, s.Name!, s.Nickname!))
            .ToListAsync(ct);
        return TypedResults.Ok(topics);
    }
}
