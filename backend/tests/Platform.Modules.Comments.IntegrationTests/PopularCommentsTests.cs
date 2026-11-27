using Platform.Testing.Time;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>E08-12: the rules of the widget «Популярные комментарии» (ADR-0105). No database.</summary>
public sealed class PopularCommentsTests
{
    private static readonly DateTimeOffset _now = TestClock.Create().GetUtcNow();
    private static readonly Guid _post = Guid.CreateVersion7(_now);
    private static readonly Guid _author = Guid.CreateVersion7(_now.AddSeconds(1));

    [Fact]
    public void PickPutsTheHigherRatedFirstAndTheEarlierAmongEquals()
    {
        var first = Candidate(0);
        var second = Candidate(1);
        var third = Candidate(2);
        var ratings = new Dictionary<Guid, int> { [first.Id] = 2, [second.Id] = 5, [third.Id] = 2 };

        var picked = PopularComments.Pick([third, first, second], ratings, count: 10);

        Assert.Equal([second.Id, first.Id, third.Id], picked.Select(p => p.Comment.Id));
        Assert.Equal([5, 2, 2], picked.Select(p => p.Rating));
    }

    [Fact]
    public void PickLeavesOutTheCommentsWithoutARating()
    {
        var rated = Candidate(0);
        var unrated = Candidate(1);
        var zero = Candidate(2);

        var picked = PopularComments.Pick(
            [rated, unrated, zero], new Dictionary<Guid, int> { [rated.Id] = 1, [zero.Id] = 0 }, count: 10);

        Assert.Equal([rated.Id], picked.Select(p => p.Comment.Id));
    }

    [Fact]
    public void PickTakesAtMostTheAskedNumber()
    {
        var candidates = Enumerable.Range(0, 5).Select(Candidate).ToArray();
        var ratings = candidates.ToDictionary(c => c.Id, _ => 1);

        var picked = PopularComments.Pick(candidates, ratings, count: 3);

        Assert.Equal(candidates.Take(3).Select(c => c.Id), picked.Select(p => p.Comment.Id));
    }

    [Fact]
    public void ReaderDoesNotSeeTheBlockedAuthorsNorTheCommentsThatLeftTheDay()
    {
        var blockedAuthor = Guid.CreateVersion7(_now.AddSeconds(2));
        var kept = View(_author, _now.AddHours(-1));
        var blocked = View(blockedAuthor, _now.AddHours(-1));
        var old = View(_author, _now - PopularComments.Window - TimeSpan.FromSeconds(1));
        var edge = View(_author, _now - PopularComments.Window);

        var shown = PopularComments.ForReader([kept, blocked, old, edge], new HashSet<Guid> { blockedAuthor }, _now, 10);

        Assert.Equal([kept.Id, edge.Id], shown.Select(c => c.Id));
    }

    [Fact]
    public void ReaderGetsAtMostTheLimitAfterTheExclusions()
    {
        var blockedAuthor = Guid.CreateVersion7(_now.AddSeconds(2));
        var views = new[]
        {
            View(blockedAuthor, _now), View(_author, _now), View(_author, _now), View(_author, _now),
        };

        var shown = PopularComments.ForReader(views, new HashSet<Guid> { blockedAuthor }, _now, limit: 2);

        Assert.Equal([views[1].Id, views[2].Id], shown.Select(c => c.Id));
    }

    private static PopularCandidate Candidate(int minutes)
    {
        var at = _now.AddMinutes(minutes);
        return new PopularCandidate(Guid.CreateVersion7(at), _post, _author, at);
    }

    private static PopularCommentView View(Guid author, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), _post, Guid.NewGuid(), "Пост", "post", author, "Текст", 1, createdAt);
}
