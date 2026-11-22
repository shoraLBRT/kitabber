using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;

namespace Platform.Modules.Blogs.Topics;

/// <summary>A topic as the editor offers it to choose from (ADR-0046).</summary>
/// <param name="Id">The topic's identifier: the <c>blogId</c> of a post in it.</param>
/// <param name="Name">Its name.</param>
/// <param name="Nickname">Its address: <c>/&lt;nickname&gt;</c>.</param>
public sealed record TopicSummary(Guid Id, string Name, string Nickname);

/// <summary>
/// <c>/api/v1/topics</c> (FR-EDITOR, ADR-0046): every topic with a name and an address, by name, open to everyone —
/// the list the editor's topic choice offers. The topic page, subscribers and the catalogue come with E11 (#76, #78).
/// </summary>
public sealed class TopicEndpoints : IApiEndpoints
{
    /// <summary>At most this many topics are listed; the catalogue with search and paging is E11-02 (#78).</summary>
    public const int MaxTopics = 500;

    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/topics").WithTags("Topics")
            .MapGet("/", ListAsync)
            .Produces<TopicSummary[]>()
            .WithName("ListTopics")
            .WithSummary("Every topic a post may be published in, by name");
    }

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
