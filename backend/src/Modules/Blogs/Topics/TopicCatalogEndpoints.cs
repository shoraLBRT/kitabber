using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;

namespace Platform.Modules.Blogs.Topics;

/// <summary>A topic as the catalogue lists it (E11-02, ADR-0088).</summary>
/// <param name="Id">The topic's identifier.</param>
/// <param name="Name">Its name.</param>
/// <param name="Nickname">Its address: <c>/&lt;nickname&gt;</c>.</param>
/// <param name="Description">What it is about; <c>null</c> for none.</param>
/// <param name="SubscriberCount">How many accounts follow it: the catalogue is sorted by it.</param>
public sealed record TopicCard(Guid Id, string Name, string Nickname, string? Description, int SubscriberCount);

/// <summary>Where the menu's topics come from (ADR-0088).</summary>
public static class TopicMenuSources
{
    /// <summary>The signed-in person's subscribed topics, by name.</summary>
    public const string Subscriptions = "subscriptions";

    /// <summary>The most followed topics: for a guest, and for a person who follows no topic yet.</summary>
    public const string Popular = "popular";
}

/// <summary>The topics of the left menu and the phone «Меню» screen (E11-02, ADR-0088).</summary>
/// <param name="Source"><c>subscriptions</c> or <c>popular</c> (<see cref="TopicMenuSources"/>).</param>
/// <param name="Topics">At most <see cref="TopicCatalogRules.MenuSize"/> topics.</param>
public sealed record TopicMenu(string Source, IReadOnlyList<TopicSummary> Topics);

/// <summary>The position after the last topic of a catalogue page: the sort key, most followed first.</summary>
public sealed record TopicCatalogCursor(int SubscriberCount, Guid Id);

/// <summary>
/// <c>/api/v1/topics/catalog</c> and <c>/api/v1/topics/menu</c> (E11-02, FR-TOPIC, FR-SUB, ADR-0088), open to everyone:
/// the catalogue behind «Показать все» — every topic, the most followed first, searched by name or address, page by
/// page with a cursor (ADR-0006); and the menu's short list — the person's subscribed topics, or the popular ones for a
/// guest. The numbers are read from the subscription rows, so they are always exact (ADR-0085).
/// </summary>
public sealed class TopicCatalogEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        var topics = v1.MapGroup("/topics").WithTags("Topics");
        topics.MapGet("/catalog", CatalogAsync)
            .Produces<CursorPage<TopicCard>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("GetTopicCatalog")
            .WithSummary("Every topic, the most followed first, searched by name or address, page by page with a cursor");
        topics.MapGet("/menu", MenuAsync)
            .Produces<TopicMenu>()
            .WithName("GetTopicMenu")
            .WithSummary("The menu's topics: the signed-in person's subscribed topics, or the most followed ones");
    }

    // Member-initialised, not positional: EF composes the filters and the order over it in SQL.
    private sealed class Row
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = "";

        public string Nickname { get; init; } = "";

        public string? Description { get; init; }

        public int SubscriberCount { get; init; }
    }

    private static IQueryable<Row> Rows(BlogsDbContext db) =>
        db.Subsites.AsNoTracking()
            .Where(s => s.Type == SubsiteType.Topic && s.Name != null && s.Nickname != null)
            .Select(s => new Row
            {
                Id = s.Id,
                Name = s.Name!,
                Nickname = s.Nickname!,
                Description = s.Description,
                SubscriberCount = db.Subscriptions.Count(x => x.SubsiteId == s.Id),
            });

    // The most followed first; among equals the newest first, so the order is complete (ADR-0006).
    private static IQueryable<Row> Popular(IQueryable<Row> rows) =>
        rows.OrderByDescending(r => r.SubscriberCount).ThenByDescending(r => r.Id);

    private static async Task<IResult> CatalogAsync(
        [FromQuery(Name = "q")] string? q,
        [AsParameters] PageQuery query,
        BlogsDbContext db,
        CancellationToken ct)
    {
        var search = TopicCatalogRules.NormalizeQuery(q);
        if (!TopicCatalogRules.IsValidQuery(search))
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["q"] = [$"Поиск — не длиннее {TopicCatalogRules.QueryMaxLength} символов."],
            });
        }

        var page = query.ToRequest<TopicCatalogCursor>();
        var rows = Rows(db);
        if (search is not null)
        {
            var pattern = TopicCatalogRules.ContainsPattern(search);
            rows = rows.Where(r =>
                EF.Functions.ILike(r.Name, pattern, @"\") || EF.Functions.ILike(r.Nickname, pattern, @"\"));
        }

        if (page.After is { } after)
        {
            rows = rows.Where(r => EF.Functions.LessThan(
                ValueTuple.Create(r.SubscriberCount, r.Id), ValueTuple.Create(after.SubscriberCount, after.Id)));
        }

        var fetched = await Popular(rows).Take(page.FetchCount).ToListAsync(ct);
        var result = page.ToPage(
            [.. fetched.Select(r => new TopicCard(r.Id, r.Name, r.Nickname, r.Description, r.SubscriberCount))],
            card => new TopicCatalogCursor(card.SubscriberCount, card.Id));
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> MenuAsync(ClaimsPrincipal user, BlogsDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is { } accountId)
        {
            var subscribed = await db.Subscriptions.AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .Join(
                    db.Subsites.Where(s => s.Type == SubsiteType.Topic && s.Name != null && s.Nickname != null),
                    x => x.SubsiteId,
                    s => s.Id,
                    (_, s) => s)
                .OrderBy(s => s.Name).ThenBy(s => s.Id)
                .Take(TopicCatalogRules.MenuSize)
                .Select(s => new TopicSummary(s.Id, s.Name!, s.Nickname!))
                .ToListAsync(ct);
            if (subscribed.Count > 0)
            {
                return TypedResults.Ok(new TopicMenu(TopicMenuSources.Subscriptions, subscribed));
            }
        }

        var popular = await Popular(Rows(db))
            .Take(TopicCatalogRules.MenuSize)
            .Select(r => new TopicSummary(r.Id, r.Name, r.Nickname))
            .ToListAsync(ct);
        return TypedResults.Ok(new TopicMenu(TopicMenuSources.Popular, popular));
    }

    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
