using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Messaging;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Blogs.Profiles;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Blogs.Topics;

/// <summary>A person in a topic's management (E11-12): an account shown by its personal blog.</summary>
/// <param name="AccountId">The account.</param>
/// <param name="BlogId">Its personal blog; <c>null</c> while it has none.</param>
/// <param name="Name">The blog's name; <c>null</c> until it is set.</param>
/// <param name="Nickname">The blog's nickname without <c>@</c>; <c>null</c> until it is set.</param>
public sealed record TopicMember(Guid AccountId, Guid? BlogId, string? Name, string? Nickname);

/// <summary>The offer to hand the topic over, waiting for the recipient's consent (E11-12, ADR-0121).</summary>
/// <param name="To">The person the topic is offered to.</param>
/// <param name="OfferedAt">When the owner made the offer.</param>
public sealed record TopicTransferView(TopicMember To, DateTimeOffset OfferedAt);

/// <summary>A topic as its management page shows it (E11-12, ADR-0121).</summary>
/// <param name="Id">The topic.</param>
/// <param name="Name">Its name.</param>
/// <param name="Nickname">Its address without the slash.</param>
/// <param name="Description">What it is about, at most 160 characters.</param>
/// <param name="Rules">Its rules, at most 2000 characters; <c>null</c> when there are none.</param>
/// <param name="Role">Who the reader is in the topic: <c>owner</c>, <c>moderator</c> or <c>none</c> (also for a guest).</param>
/// <param name="Owner">The owner; <c>null</c> for a topic without one.</param>
/// <param name="Moderators">The moderators the owner appointed, in the order of appointment.</param>
/// <param name="Transfer">
/// The offer to hand the topic over; only its owner and the person it is offered to see it, <c>null</c> for everyone else
/// and when there is none.
/// </param>
/// <param name="TransferToYou">The topic is offered to the reader, who may accept or decline it.</param>
public sealed record TopicManagementView(
    Guid Id,
    string? Name,
    string? Nickname,
    string? Description,
    string? Rules,
    string Role,
    TopicMember? Owner,
    IReadOnlyList<TopicMember> Moderators,
    TopicTransferView? Transfer,
    bool TransferToYou);

/// <summary>What the owner edits in a topic (E11-12). Both fields are replaced: missing or empty clears them.</summary>
/// <param name="Description">What it is about, at most 160 characters.</param>
/// <param name="Rules">Its rules, at most 2000 characters.</param>
public sealed record TopicSettingsInput(string? Description = null, string? Rules = null);

/// <summary>A person named by the nickname of their personal blog (E11-12): a new moderator or the recipient of the topic.</summary>
/// <param name="Nickname">The nickname, with or without <c>@</c>, any case.</param>
public sealed record TopicMemberInput(string? Nickname);

/// <summary>
/// <c>/api/v1/topics/{id}/…</c> (E11-12, FR-TOPIC, ADR-0121): the management of a topic. Everyone reads its rules and
/// moderators; the owner edits the description, the rules and the cover, appoints and removes moderators and offers the
/// topic to another person, who accepts or declines the offer. What moderators may do comes with E11-09 (#174).
/// </summary>
public sealed class TopicManagementEndpoints : IApiEndpoints
{
    public const string TopicNotFoundCode = "blogs.topic_not_found";

    /// <summary>The problem code of an action only the topic's owner may take.</summary>
    public const string NotTopicOwnerCode = "blogs.not_topic_owner";

    /// <summary>The problem code of accepting or declining an offer the reader was not made.</summary>
    public const string NoTransferOfferCode = "blogs.no_transfer_offer";

    private const string _noSuchBlog = "Нет блога с таким никнеймом.";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var topic = v1.MapGroup("/topics/{id:guid}").WithTags("Topics");
        topic.MapGet("/management", GetAsync)
            .Produces<TopicManagementView>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetTopicManagement")
            .WithSummary("The rules and moderators of a topic, the reader's role in it and an offer to hand it over");
        topic.MapPut("/settings", UpdateAsync)
            .RequireAuthorization()
            .Produces<TopicManagementView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("UpdateTopicSettings")
            .WithSummary("Replaces the description and the rules of a topic the signed-in account owns");
        topic.MapPut("/cover", SetCoverAsync)
            .RequireAuthorization()
            .Produces<Profile>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("SetTopicCover")
            .WithSummary("Sets the cover of a topic the signed-in account owns, or moves it to a new position");
        topic.MapDelete("/cover", RemoveCoverAsync)
            .RequireAuthorization()
            .Produces<Profile>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("RemoveTopicCover")
            .WithSummary("Removes the cover of a topic the signed-in account owns");
        topic.MapPost("/moderators", AppointAsync)
            .RequireAuthorization()
            .Produces<TopicManagementView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("AppointTopicModerator")
            .WithSummary("Appoints a moderator of a topic the signed-in account owns, by the nickname of their blog");
        topic.MapDelete("/moderators/{accountId:guid}", RemoveModeratorAsync)
            .RequireAuthorization()
            .Produces<TopicManagementView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("RemoveTopicModerator")
            .WithSummary("Removes a moderator of a topic the signed-in account owns");
        topic.MapPut("/transfer", OfferAsync)
            .RequireAuthorization()
            .Produces<TopicManagementView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("OfferTopicTransfer")
            .WithSummary("Offers a topic the signed-in account owns to another person; it changes hands when they accept");
        topic.MapDelete("/transfer", WithdrawAsync)
            .RequireAuthorization()
            .Produces<TopicManagementView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("WithdrawTopicTransfer")
            .WithSummary("Cancels the offer to hand a topic over (its owner) or declines it (the person it is offered to)");
        topic.MapPost("/transfer/accept", AcceptAsync)
            .RequireAuthorization()
            .Produces<TopicManagementView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("AcceptTopicTransfer")
            .WithSummary("Accepts the offer of a topic: the signed-in account becomes its owner");
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct) =>
        await FindTopicAsync(db, id, ct) is { } topic
            ? TypedResults.Ok(await ViewAsync(db, topic, ProfileEndpoints.AccountOf(user), ct))
            : ApiProblems.NotFound(TopicNotFoundCode);

    private static async Task<IResult> UpdateAsync(
        Guid id,
        TopicSettingsInput input,
        ClaimsPrincipal user,
        BlogsDbContext db,
        IModuleOutbox<BlogsDbContext> outbox,
        TimeProvider time,
        CancellationToken ct)
    {
        var (topic, account, problem) = await OwnTopicAsync(db, id, user, ct);
        if (problem is not null)
        {
            return problem;
        }

        var errors = new Dictionary<string, string[]>();
        if (!SubsiteRules.IsValidDescription(input.Description))
        {
            errors["description"] = [$"Описание — не длиннее {SubsiteRules.DescriptionMaxLength} символов."];
        }

        if (!SubsiteRules.IsValidRules(input.Rules))
        {
            errors["rules"] = [$"Правила — не длиннее {SubsiteRules.RulesMaxLength} символов."];
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        topic!.Describe(input.Description);
        topic.SetRules(input.Rules);
        // Search finds a topic by its description (ADR-0094); the event goes with the change.
        await outbox.Publish(new BlogChanged(topic.Id, time.GetUtcNow()), ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(db, topic, account, ct));
    }

    private static async Task<IResult> SetCoverAsync(
        Guid id, CoverInput input, ClaimsPrincipal user, BlogsDbContext db, IMediaLibrary media, CancellationToken ct)
    {
        var (topic, account, problem) = await OwnTopicAsync(db, id, user, ct);
        if (problem is not null)
        {
            return problem;
        }

        var errors = new Dictionary<string, string[]>();
        if (await ProfileEndpoints.ImageProblemAsync(input.MediaId, account!.Value, "Обложка — это изображение.", media, ct)
            is { } coverProblem)
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

        topic!.SetCover(input.MediaId, input.Position);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ProfileEndpoints.ViewAsync(topic, account, db, media, ct));
    }

    private static async Task<IResult> RemoveCoverAsync(
        Guid id, ClaimsPrincipal user, BlogsDbContext db, IMediaLibrary media, CancellationToken ct)
    {
        var (topic, account, problem) = await OwnTopicAsync(db, id, user, ct);
        if (problem is not null)
        {
            return problem;
        }

        topic!.RemoveCover();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ProfileEndpoints.ViewAsync(topic, account, db, media, ct));
    }

    private static async Task<IResult> AppointAsync(
        Guid id, TopicMemberInput input, ClaimsPrincipal user, BlogsDbContext db, TimeProvider time, CancellationToken ct)
    {
        var (topic, account, problem) = await OwnTopicAsync(db, id, user, ct);
        if (problem is not null)
        {
            return problem;
        }

        if (await MemberByNicknameAsync(db, input.Nickname, ct) is not { } candidate)
        {
            return NicknameProblem(_noSuchBlog);
        }

        var moderators = await db.TopicModerators.Where(m => m.SubsiteId == topic!.Id).Select(m => m.AccountId).ToListAsync(ct);
        if (TopicManagementRules.ModeratorProblem(candidate, account!.Value, moderators) is { } moderatorProblem)
        {
            return NicknameProblem(moderatorProblem);
        }

        db.TopicModerators.Add(new TopicModerator { SubsiteId = topic!.Id, AccountId = candidate, AppointedAt = time.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same person appointed twice at once: the first appointment stands.
            db.ChangeTracker.Clear();
        }

        return TypedResults.Ok(await ViewAsync(db, topic, account, ct));
    }

    private static async Task<IResult> RemoveModeratorAsync(
        Guid id, Guid accountId, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct)
    {
        var (topic, account, problem) = await OwnTopicAsync(db, id, user, ct);
        if (problem is not null)
        {
            return problem;
        }

        // Removing someone who is not a moderator changes nothing.
        await db.TopicModerators.Where(m => m.SubsiteId == topic!.Id && m.AccountId == accountId).ExecuteDeleteAsync(ct);
        return TypedResults.Ok(await ViewAsync(db, topic!, account, ct));
    }

    // A new offer replaces the old one and notifies its recipient (ADR-0121); the topic stays the owner's until accepted.
    private static async Task<IResult> OfferAsync(
        Guid id,
        TopicMemberInput input,
        ClaimsPrincipal user,
        BlogsDbContext db,
        IModuleOutbox<BlogsDbContext> outbox,
        TimeProvider time,
        CancellationToken ct)
    {
        var (topic, account, problem) = await OwnTopicAsync(db, id, user, ct);
        if (problem is not null)
        {
            return problem;
        }

        if (await MemberByNicknameAsync(db, input.Nickname, ct) is not { } recipient)
        {
            return NicknameProblem(_noSuchBlog);
        }

        if (TopicManagementRules.TransferProblem(recipient, account!.Value) is { } transferProblem)
        {
            return NicknameProblem(transferProblem);
        }

        var current = await db.TopicTransferOffers.SingleOrDefaultAsync(o => o.SubsiteId == topic!.Id, ct);
        if (current?.ToAccountId == recipient)
        {
            // The same offer again: nothing changes and nobody is notified twice.
            return TypedResults.Ok(await ViewAsync(db, topic!, account, ct));
        }

        if (current is not null)
        {
            db.TopicTransferOffers.Remove(current);
            await db.SaveChangesAsync(ct);
        }

        var now = time.GetUtcNow();
        db.TopicTransferOffers.Add(new TopicTransferOffer
        {
            SubsiteId = topic!.Id,
            FromAccountId = account.Value,
            ToAccountId = recipient,
            OfferedAt = now,
        });
        await outbox.Publish(new TopicTransferOffered(topic.Id, account.Value, recipient, now), ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(db, topic, account, ct));
    }

    // The owner cancels the offer, or the person it is made to declines it; anyone else is refused.
    private static async Task<IResult> WithdrawAsync(Guid id, ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct)
    {
        if (ProfileEndpoints.AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await FindTopicAsync(db, id, ct) is not { } topic)
        {
            return ApiProblems.NotFound(TopicNotFoundCode);
        }

        var offer = await db.TopicTransferOffers.SingleOrDefaultAsync(o => o.SubsiteId == topic.Id, ct);
        if (topic.OwnerAccountId != account && offer?.ToAccountId != account)
        {
            return ApiProblems.Forbidden(NotTopicOwnerCode, "Отменить передачу может только владелец темы.");
        }

        if (offer is not null)
        {
            db.TopicTransferOffers.Remove(offer);
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(await ViewAsync(db, topic, account, ct));
    }

    // The recipient's consent (ADR-0121): the topic becomes theirs, the offer is gone, and a new owner is not its own
    // moderator. The former owner keeps no role; the new owner appoints moderators again if they want.
    private static async Task<IResult> AcceptAsync(
        Guid id,
        ClaimsPrincipal user,
        BlogsDbContext db,
        IModuleOutbox<BlogsDbContext> outbox,
        TopicLimits limits,
        TimeProvider time,
        CancellationToken ct)
    {
        if (ProfileEndpoints.AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (await FindTopicAsync(db, id, ct) is not { } topic)
        {
            return ApiProblems.NotFound(TopicNotFoundCode);
        }

        var offer = await db.TopicTransferOffers.SingleOrDefaultAsync(o => o.SubsiteId == topic.Id, ct);
        if (offer is null || offer.ToAccountId != account || offer.FromAccountId != topic.OwnerAccountId)
        {
            return ApiProblems.Forbidden(NoTransferOfferCode, "Эту тему вам не предлагали.");
        }

        // The limit of topics per account (ADR-0084) holds for a topic received as for a topic created.
        var owned = await db.Subsites.CountAsync(s => s.Type == SubsiteType.Topic && s.OwnerAccountId == account, ct);
        if (owned >= limits.MaxPerAccount)
        {
            return ApiProblems.Conflict(
                TopicEndpoints.TopicLimitReachedCode, $"У одного аккаунта может быть не больше {limits.MaxPerAccount} тем.");
        }

        topic.TransferTo(account);
        db.TopicTransferOffers.Remove(offer);
        db.TopicModerators.RemoveRange(
            await db.TopicModerators.Where(m => m.SubsiteId == topic.Id && m.AccountId == account).ToListAsync(ct));
        await outbox.Publish(new BlogChanged(topic.Id, time.GetUtcNow()), ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(db, topic, account, ct));
    }

    private static Task<Subsite?> FindTopicAsync(BlogsDbContext db, Guid id, CancellationToken ct) =>
        db.Subsites.SingleOrDefaultAsync(s => s.Id == id && s.Type == SubsiteType.Topic, ct);

    // The signed-in owner's topic, or the answer that refuses the request.
    private static async Task<(Subsite? Topic, Guid? Account, IResult? Problem)> OwnTopicAsync(
        BlogsDbContext db, Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        if (ProfileEndpoints.AccountOf(user) is not { } account)
        {
            return (null, null, TypedResults.Unauthorized());
        }

        if (await FindTopicAsync(db, id, ct) is not { } topic)
        {
            return (null, account, ApiProblems.NotFound(TopicNotFoundCode));
        }

        return topic.OwnerAccountId == account
            ? (topic, account, null)
            : (topic, account, ApiProblems.Forbidden(NotTopicOwnerCode, "Управлять темой может только её владелец."));
    }

    // A person is named by their personal blog's nickname; a topic's nickname names no one.
    private static async Task<Guid?> MemberByNicknameAsync(BlogsDbContext db, string? nickname, CancellationToken ct) =>
        SubsiteRules.NormalizeNickname(nickname) is { } normalized
            ? await db.Subsites.AsNoTracking()
                .Where(s => s.Type == SubsiteType.Personal && s.Nickname == normalized)
                .Select(s => s.OwnerAccountId)
                .SingleOrDefaultAsync(ct)
            : null;

    private static IResult NicknameProblem(string message) =>
        ApiProblems.Validation(new Dictionary<string, string[]> { ["nickname"] = [message] });

    private static async Task<TopicManagementView> ViewAsync(
        BlogsDbContext db, Subsite topic, Guid? viewer, CancellationToken ct)
    {
        var moderatorIds = await db.TopicModerators.AsNoTracking()
            .Where(m => m.SubsiteId == topic.Id)
            .OrderBy(m => m.AppointedAt).ThenBy(m => m.AccountId)
            .Select(m => m.AccountId)
            .ToListAsync(ct);
        var offer = await db.TopicTransferOffers.AsNoTracking().SingleOrDefaultAsync(o => o.SubsiteId == topic.Id, ct);
        var role = TopicManagementRules.RoleOf(viewer, topic.OwnerAccountId, viewer is { } v && moderatorIds.Contains(v));
        var toYou = offer is not null && viewer == offer.ToAccountId;
        var seesOffer = offer is not null && (role == TopicRole.Owner || toYou);
        var accounts = moderatorIds
            .Concat(topic.OwnerAccountId is { } owner ? [owner] : [])
            .Concat(seesOffer ? [offer!.ToAccountId] : [])
            .Distinct()
            .ToList();
        var blogs = await db.Subsites.AsNoTracking()
            .Where(s => s.Type == SubsiteType.Personal && s.OwnerAccountId != null && accounts.Contains(s.OwnerAccountId.Value))
            .ToDictionaryAsync(s => s.OwnerAccountId!.Value, ct);
        TopicMember Member(Guid account) => blogs.TryGetValue(account, out var blog)
            ? new TopicMember(account, blog.Id, blog.Name, blog.Nickname)
            : new TopicMember(account, null, null, null);

        return new TopicManagementView(
            topic.Id,
            topic.Name,
            topic.Nickname,
            topic.Description,
            topic.Rules,
            TopicManagementRules.NameOf(role),
            topic.OwnerAccountId is { } ownerId ? Member(ownerId) : null,
            [.. moderatorIds.Select(Member)],
            seesOffer ? new TopicTransferView(Member(offer!.ToAccountId), offer.OfferedAt) : null,
            toYou);
    }
}
