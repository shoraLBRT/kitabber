using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;

namespace Platform.Api.Tests;

/// <summary>A stand-in module that exercises the conventions the way a real module will.</summary>
internal sealed class TestModule : IApiEndpoints
{
    public const string AdminPolicy = "admin";

    /// <summary>45 items, several sharing a timestamp, so pages break inside a run of equal sort keys.</summary>
    public static readonly IReadOnlyList<TestItem> Items = Enumerable.Range(1, 45)
        .Select(i => new TestItem(i, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i / 4)))
        .ToArray();

    public static void Register(IServiceCollection services)
    {
        // Each module calls AddValidation itself: the source generator registers the validators of its own assembly.
        services.AddValidation();
        services.AddApiEndpoints<TestModule>();
        services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        services.AddAuthorizationBuilder().AddPolicy(AdminPolicy, policy => policy.RequireRole("admin"));
    }

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var test = v1.MapGroup("/test");

        // Newest first, ties broken by id: the keyset is (CreatedAt, Id).
        test.MapGet("/items", ([AsParameters] PageQuery query) =>
        {
            var page = query.ToRequest<TestItemCursor>();
            var after = page.After;
            var rows = Items
                .Where(i => after is null || i.CreatedAt < after.CreatedAt
                    || (i.CreatedAt == after.CreatedAt && i.Id < after.Id))
                .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
                .Take(page.FetchCount)
                .ToList();
            return page.ToPage(rows, i => new TestItemCursor(i.CreatedAt, i.Id));
        });

        test.MapPost("/items", (CreateItem item) => TypedResults.Created($"/api/v1/test/items/{item.Name}", item));
        test.MapGet("/conflict", IResult () =>
            throw new ApiProblemException(StatusCodes.Status409Conflict, "test.already_exists", "Already exists"));
        test.MapGet("/crash", IResult () => throw new InvalidOperationException("secret internals"));
        test.MapGet("/admin", () => TypedResults.Ok()).RequireAuthorization(AdminPolicy);
    }
}

internal sealed record TestItem(int Id, DateTimeOffset CreatedAt);

internal sealed record TestItemCursor(DateTimeOffset CreatedAt, int Id);

public sealed record CreateItem([Required, StringLength(10)] string Name);

/// <summary>Authenticates a request that carries <c>X-Test-Role</c>, with that role.</summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "tester"), new Claim(ClaimTypes.Role, role.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
