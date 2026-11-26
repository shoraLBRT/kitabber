using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Http;
using Platform.Http.Problems;

namespace Platform.Modules.Preferences.Blocks;

/// <summary>An account in the black list.</summary>
/// <param name="AccountId">The blocked account.</param>
/// <param name="BlockedAt">When it was blocked.</param>
public sealed record BlockedAccountView(Guid AccountId, DateTimeOffset BlockedAt);

/// <summary>The black list of the signed-in account (E11-06).</summary>
/// <param name="Accounts">The blocked accounts, the latest blocked first.</param>
/// <param name="Max">The most accounts the list may hold.</param>
public sealed record BlockListView(IReadOnlyList<BlockedAccountView> Accounts, int Max);

/// <summary>
/// The black list of the signed-in account (E11-06, FR-SUB, ADR-0091): read it, block an account and unblock it. Both
/// changes may be repeated with the same result. A blocked account's posts leave the person's feeds, its comments come
/// hidden, and it may not reply to the person's comments nor comment under their posts. No one reads or changes
/// another's list.
/// </summary>
public sealed class BlockEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        var own = v1.MapGroup("/preferences/me/blocks").WithTags("Preferences").RequireAuthorization();
        own.MapGet("/", GetAsync)
            .Produces<BlockListView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetOwnBlockList")
            .WithSummary("The black list of the signed-in account, the latest blocked first");
        own.MapPut("/{accountId:guid}", BlockAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("BlockAccount")
            .WithSummary("Adds an account to the black list of the signed-in account");
        own.MapDelete("/{accountId:guid}", UnblockAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("UnblockAccount")
            .WithSummary("Removes an account from the black list of the signed-in account");
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, PreferencesDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        var accounts = await db.BlockedAccounts.AsNoTracking()
            .Where(b => b.OwnerAccountId == owner)
            .OrderByDescending(b => b.BlockedAt).ThenBy(b => b.AccountId)
            .Select(b => new BlockedAccountView(b.AccountId, b.BlockedAt))
            .ToListAsync(ct);
        return TypedResults.Ok(new BlockListView(accounts, BlockRules.MaxBlocked));
    }

    private static async Task<IResult> BlockAsync(
        Guid accountId, ClaimsPrincipal user, PreferencesDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        if (await db.BlockedAccounts.AnyAsync(b => b.OwnerAccountId == owner && b.AccountId == accountId, ct))
        {
            return TypedResults.NoContent();
        }

        var count = await db.BlockedAccounts.CountAsync(b => b.OwnerAccountId == owner, ct);
        if (BlockRules.BlockProblem(owner, accountId, count) is { } problem)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["accountId"] = [problem],
            });
        }

        db.BlockedAccounts.Add(new BlockedAccount
        {
            OwnerAccountId = owner,
            AccountId = accountId,
            BlockedAt = time.GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        })
        {
            // Two devices blocking at the same moment: the other one already did it.
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> UnblockAsync(
        Guid accountId, ClaimsPrincipal user, PreferencesDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } owner)
        {
            return TypedResults.Unauthorized();
        }

        await db.BlockedAccounts
            .Where(b => b.OwnerAccountId == owner && b.AccountId == accountId)
            .ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
