using Platform.Http.Pagination;
using Platform.Http.Problems;

namespace Platform.Api.Tests;

/// <summary>Unit tests of the shared cursor pagination helper.</summary>
public sealed class CursorPaginationTests
{
    private sealed record Position(DateTimeOffset CreatedAt, int Id);

    [Fact]
    public void CursorRoundTrips()
    {
        var position = new Position(new DateTimeOffset(2026, 11, 19, 10, 30, 0, TimeSpan.Zero), 42);

        var cursor = CursorCodec.Encode(position);

        Assert.Equal(position, CursorCodec.Decode<Position>(cursor));
        Assert.DoesNotContain("+", cursor, StringComparison.Ordinal);
        Assert.DoesNotContain("/", cursor, StringComparison.Ordinal);
        Assert.DoesNotContain("=", cursor, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not a cursor!")]
    [InlineData("bnVsbA")] // "null"
    [InlineData("eyJpZCI6")] // truncated JSON
    [InlineData("WzEsMl0")] // [1,2]: JSON of another shape
    public void MalformedCursorIsInvalidCursor(string cursor)
    {
        var problem = Assert.Throws<ApiProblemException>(() => new PageQuery(cursor).ToRequest<Position>());

        Assert.Equal(400, problem.Status);
        Assert.Equal(ApiErrorCodes.InvalidCursor, problem.Code);
    }

    [Fact]
    public void LimitDefaultsAndFetchesOneMore()
    {
        var request = new PageQuery().ToRequest<Position>();

        Assert.Null(request.After);
        Assert.Equal(PageQuery.DefaultLimit, request.Limit);
        Assert.Equal(PageQuery.DefaultLimit + 1, request.FetchCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(PageQuery.MaxLimit + 1)]
    public void LimitOutOfRangeIsValidationFailed(int limit)
    {
        var problem = Assert.Throws<ApiProblemException>(() => new PageQuery(Limit: limit).ToRequest<Position>());

        Assert.Equal(400, problem.Status);
        Assert.Equal(ApiErrorCodes.ValidationFailed, problem.Code);
        Assert.True(problem.Extensions.ContainsKey("errors"));
    }

    [Fact]
    public void ShortReadIsTheLastPage()
    {
        var request = new PageQuery(Limit: 3).ToRequest<Position>();

        var page = request.ToPage([1, 2, 3], i => new Position(DateTimeOffset.UnixEpoch, i));

        Assert.Equal([1, 2, 3], page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void FullReadReturnsLimitItemsAndTheCursorOfTheLast()
    {
        var request = new PageQuery(Limit: 3).ToRequest<Position>();

        var page = request.ToPage([1, 2, 3, 4], i => new Position(DateTimeOffset.UnixEpoch, i));

        Assert.Equal([1, 2, 3], page.Items);
        Assert.NotNull(page.NextCursor);
        Assert.Equal(new Position(DateTimeOffset.UnixEpoch, 3), CursorCodec.Decode<Position>(page.NextCursor));
    }
}
