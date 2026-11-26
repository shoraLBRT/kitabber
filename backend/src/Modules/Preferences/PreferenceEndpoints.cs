using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Http;
using Platform.Http.Problems;

namespace Platform.Modules.Preferences;

/// <summary>The section «Основные» of the settings.</summary>
/// <param name="Theme">The colour theme: <c>system</c>, <c>light</c> or <c>dark</c>.</param>
public sealed record GeneralPreferencesView(string Theme);

/// <summary>The settings of the signed-in account that every device applies (ADR-0070).</summary>
/// <param name="General">The section «Основные».</param>
public sealed record PreferencesView(GeneralPreferencesView General);

/// <summary>A new choice in the section «Основные».</summary>
/// <param name="Theme">The colour theme: <c>system</c>, <c>light</c> or <c>dark</c>.</param>
public sealed record GeneralPreferencesInput(string? Theme);

/// <summary>
/// The settings of the signed-in account (E04-08, ADR-0070): read on every device and changed by section. An account
/// that never changed anything gets the defaults. Any signed-in account may change its own settings; no one reads or
/// changes another's.
/// </summary>
public sealed class PreferenceEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1)
    {
        var own = v1.MapGroup("/preferences").WithTags("Preferences").RequireAuthorization();
        own.MapGet("/me", GetAsync)
            .Produces<PreferencesView>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetOwnPreferences")
            .WithSummary("The settings of the signed-in account, or the defaults when it never changed them");
        own.MapPut("/me/general", PutGeneralAsync)
            .Produces<PreferencesView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("PutOwnGeneralPreferences")
            .WithSummary("Changes the section «Основные» of the signed-in account's settings");
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, PreferencesDbContext db, CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var saved = await db.AccountPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.AccountId == account, ct);
        return TypedResults.Ok(View(saved ?? AccountPreferences.For(account)));
    }

    private static async Task<IResult> PutGeneralAsync(
        GeneralPreferencesInput input,
        ClaimsPrincipal user,
        PreferencesDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        if (AccountOf(user) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        if (PreferenceRules.ParseTheme(input.Theme) is not { } theme)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["theme"] = ["Тема — system, light или dark."],
            });
        }

        // Two devices saving at the same moment: the insert that loses becomes an update of the row the other wrote.
        for (var attempt = 0; ; attempt++)
        {
            var saved = await db.AccountPreferences.SingleOrDefaultAsync(p => p.AccountId == account, ct);
            if (saved is null)
            {
                saved = AccountPreferences.For(account);
                db.AccountPreferences.Add(saved);
            }

            saved.Theme = theme;
            saved.UpdatedAt = time.GetUtcNow();
            try
            {
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(View(saved));
            }
            catch (DbUpdateException e) when (attempt == 0 && e.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            })
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private static PreferencesView View(AccountPreferences preferences) =>
        new(new GeneralPreferencesView(PreferenceRules.NameOf(preferences.Theme)));

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    private static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
