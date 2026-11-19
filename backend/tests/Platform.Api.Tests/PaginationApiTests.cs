using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Platform.Api.Tests;

/// <summary>A paginated list over HTTP: the shape a client sees.</summary>
public sealed class PaginationApiTests : IClassFixture<ProblemDetailsTests.Host>
{
    private readonly HttpClient _client;

    public PaginationApiTests(ProblemDetailsTests.Host host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _client = host.CreateClient();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(45)]
    [InlineData(100)]
    public async Task WalkingTheCursorsReturnsEveryItemOnceInOrder(int limit)
    {
        var expected = TestModule.Items
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Select(i => i.Id)
            .ToList();

        var seen = new List<int>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var url = $"/api/v1/test/items?limit={limit}" + (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            var page = await _client.GetFromJsonAsync<JsonElement>(url, TestContext.Current.CancellationToken);
            var items = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
            Assert.True(items.Count <= limit);
            seen.AddRange(items);
            cursor = page.GetProperty("nextCursor").GetString();
            pages++;
        }
        while (cursor is not null && pages < 100);

        Assert.Equal(expected, seen);
        Assert.Equal((int)Math.Ceiling(expected.Count / (double)limit), pages);
    }

    [Fact]
    public async Task TamperedCursorIsInvalidCursor()
    {
        using var response = await _client.GetAsync("/api/v1/test/items?cursor=garbage", TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_cursor", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task LimitOutOfRangeIsValidationFailed()
    {
        using var response = await _client.GetAsync("/api/v1/test/items?limit=0", TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("limit", out _));
    }
}
