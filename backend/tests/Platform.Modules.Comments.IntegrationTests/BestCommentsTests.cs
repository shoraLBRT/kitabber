using Platform.Modules.Comments.Contracts;
using Platform.Testing.Time;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>E08-15: the choice of the best comment of a feed card and its excerpt (ADR-0062). No database.</summary>
public sealed class BestCommentsTests
{
    private static readonly DateTimeOffset _now = TestClock.Create().GetUtcNow();
    private static readonly Guid _post = Guid.CreateVersion7(_now);
    private static readonly Guid _other = Guid.CreateVersion7(_now.AddSeconds(1));

    [Fact]
    public void HighestRatedWinsAndTheEarlierAmongEquals()
    {
        var early = Candidate(_post, 1);
        var liked = Candidate(_post, 2);
        var tiedLater = Candidate(_post, 3);
        var plain = Candidate(_other, 4);
        var plainLater = Candidate(_other, 5);

        var best = BestComments.Pick(
            [tiedLater, liked, early, plainLater, plain],
            new Dictionary<Guid, int> { [liked.Id] = 2, [tiedLater.Id] = 2 });

        Assert.Equal(liked, best[_post]);
        Assert.Equal(plain, best[_other]);
    }

    [Fact]
    public void SameMomentFallsBackToTheIdAndTwiceGivenCandidateCountsOnce()
    {
        var a = new BestCandidate(Guid.CreateVersion7(_now), _post, _now);
        var b = new BestCandidate(Guid.CreateVersion7(_now.AddMilliseconds(1)), _post, _now);
        var (first, second) = a.Id.CompareTo(b.Id) < 0 ? (a, b) : (b, a);

        var best = BestComments.Pick([second, first, second], new Dictionary<Guid, int>());

        Assert.Equal(first, best[_post]);
    }

    [Fact]
    public void NoCandidatesNoBest() =>
        Assert.Empty(BestComments.Pick([], new Dictionary<Guid, int>()));

    [Fact]
    public void ShortTextIsWhole() => Assert.Equal("Коротко.", BestComments.Excerpt("Коротко."));

    [Fact]
    public void LongTextIsCutWithAnEllipsis()
    {
        var text = new string('а', BestComment.ExcerptLength - 1) + " " + new string('б', 50);

        var excerpt = BestComments.Excerpt(text);

        Assert.Equal(new string('а', BestComment.ExcerptLength - 1) + "…", excerpt);
    }

    [Fact]
    public void CutNeverSplitsACharacterOutsideTheBasicPlane()
    {
        var text = new string('а', BestComment.ExcerptLength - 1) + "😀" + "хвост";

        var excerpt = BestComments.Excerpt(text);

        Assert.Equal(new string('а', BestComment.ExcerptLength - 1) + "…", excerpt);
    }

    private static BestCandidate Candidate(Guid post, int minutes) =>
        new(Guid.CreateVersion7(_now.AddMinutes(minutes)), post, _now.AddMinutes(minutes));
}
