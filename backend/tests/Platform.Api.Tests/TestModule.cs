using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Http.Pagination;
using Platform.Http.Problems;
using Platform.Testing.Api;

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
        services.AddTestAuthentication();
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

