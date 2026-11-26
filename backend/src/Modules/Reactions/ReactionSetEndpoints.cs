using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;

namespace Platform.Modules.Reactions;

/// <summary>One reaction the platform offers (E02-12, ADR-0095).</summary>
/// <param name="Code">The code of the reaction and of its picture.</param>
/// <param name="Title">The name a person sees and a screen reader reads.</param>
public sealed record ReactionSetItem(string Code, string Title);

/// <summary>
/// The set of reactions from the directory (E02-12, FR-REACT, ADR-0095): the offered ones in their order. Reading it
/// needs no account; the administration edits the directory (E15-12, #168).
/// </summary>
public sealed class ReactionSetEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGet("/reactions/set", GetSetAsync)
            .WithTags("Reactions")
            .Produces<IReadOnlyList<ReactionSetItem>>()
            .WithName("GetReactionSet")
            .WithSummary("The reactions the platform offers, in their order");
    }

    private static async Task<IResult> GetSetAsync(ReactionsDbContext db, CancellationToken cancellationToken)
    {
        var entries = await db.ReactionSet.AsNoTracking()
            .Where(e => e.Enabled)
            .OrderBy(e => e.Position).ThenBy(e => e.Code)
            .Select(e => new { e.Code, e.Title })
            .ToListAsync(cancellationToken);
        // A code without a picture in this build is not offered: the client could not show it (ADR-0095).
        IReadOnlyList<ReactionSetItem> items = [.. entries
            .Where(e => ReactionGraphics.Has(e.Code))
            .Select(e => new ReactionSetItem(e.Code, e.Title))];
        return Results.Ok(items);
    }
}
