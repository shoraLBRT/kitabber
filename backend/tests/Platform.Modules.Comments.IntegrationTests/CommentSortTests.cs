using Platform.Testing.Time;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>E09-04: the order of the threads under a post, «Популярные» and «По дате» (ADR-0058). No database.</summary>
public sealed class CommentSortTests
{
    private static readonly DateTimeOffset _now = TestClock.Create().GetUtcNow();
    private static readonly Guid _post = Guid.CreateVersion7(_now);
    private static readonly Guid _author = Guid.CreateVersion7(_now.AddSeconds(1));

    [Fact]
    public void PopularPutsTheThreadsWithMoreRepliesFirstAndTheOlderAmongEquals()
    {
        var (tree, quiet, busy, lively, busyReply, busyDeeper, livelyReply) = Threads();

        var sorted = CommentTree.Sorted(CommentTree.Shown(tree, 0, levels: null), CommentSort.Popular);

        Assert.Equal(
            new[] { busy, busyReply, busyDeeper, lively, livelyReply, quiet }.Select(c => c.Id),
            sorted.Select(s => s.Comment.Id));
        Assert.Equal([2, 1, 0, 1, 0, 0], sorted.Select(s => s.Replies));
    }

    [Fact]
    public void PopularKeepsTheOlderThreadFirstWhenRepliesAreEqual()
    {
        var first = Comment.CreateNative(_author, _post, parent: null, "1", _now);
        var second = Comment.CreateNative(_author, _post, parent: null, "2", _now.AddMinutes(1));
        var tree = new[] { first, second };

        var sorted = CommentTree.Sorted(CommentTree.Shown(tree, 0, levels: null), CommentSort.Popular);

        Assert.Equal([first.Id, second.Id], sorted.Select(s => s.Comment.Id));
    }

    [Fact]
    public void PopularPutsTheHigherRatedThreadFirstThenTheOneWithMoreReplies()
    {
        var (tree, quiet, busy, lively, busyReply, busyDeeper, livelyReply) = Threads();
        var ratings = new Dictionary<Guid, int> { [quiet.Id] = 3, [lively.Id] = 1, [busyReply.Id] = 9 };

        var sorted = CommentTree.Sorted(CommentTree.Shown(tree, 0, levels: null), CommentSort.Popular, ratings);

        // The rating of a reply does not move its thread: threads are ordered by their first comment (E10-05).
        Assert.Equal(
            new[] { quiet, lively, livelyReply, busy, busyReply, busyDeeper }.Select(c => c.Id),
            sorted.Select(s => s.Comment.Id));
    }

    [Fact]
    public void DateIgnoresTheRatings()
    {
        var (tree, quiet, busy, lively, busyReply, busyDeeper, livelyReply) = Threads();

        var sorted = CommentTree.Sorted(
            CommentTree.Shown(tree, 0, levels: null), CommentSort.Date, new Dictionary<Guid, int> { [quiet.Id] = 5 });

        Assert.Equal(
            new[] { lively, livelyReply, busy, busyReply, busyDeeper, quiet }.Select(c => c.Id),
            sorted.Select(s => s.Comment.Id));
    }

    [Fact]
    public void DatePutsTheNewestThreadFirstAndKeepsTheOrderOfReplies()
    {
        var (tree, quiet, busy, lively, busyReply, busyDeeper, livelyReply) = Threads();

        var sorted = CommentTree.Sorted(CommentTree.Shown(tree, 0, levels: null), CommentSort.Date);

        Assert.Equal(
            new[] { lively, livelyReply, busy, busyReply, busyDeeper, quiet }.Select(c => c.Id),
            sorted.Select(s => s.Comment.Id));
    }

    [Fact]
    public void SortCountsTheRepliesALevelLimitLeftOut()
    {
        var (tree, quiet, busy, lively, _, _, _) = Threads();

        var sorted = CommentTree.Sorted(CommentTree.Shown(tree, 0, levels: 1), CommentSort.Popular);

        Assert.Equal([busy.Id, lively.Id, quiet.Id], sorted.Select(s => s.Comment.Id));
    }

    [Fact]
    public void NoCommentsStayNoComments() =>
        Assert.Empty(CommentTree.Sorted([], CommentSort.Date));

    // Three threads in the order of the tree: «quiet» (no replies), «busy» (two replies, one under the other), «lively»
    // (one reply), written in that order.
    private static (Comment[] Tree, Comment Quiet, Comment Busy, Comment Lively, Comment BusyReply, Comment BusyDeeper,
        Comment LivelyReply) Threads()
    {
        var quiet = Comment.CreateNative(_author, _post, parent: null, "тихий", _now);
        var busy = Comment.CreateNative(_author, _post, parent: null, "обсуждаемый", _now.AddMinutes(1));
        var lively = Comment.CreateNative(_author, _post, parent: null, "живой", _now.AddMinutes(2));
        var busyReply = Comment.CreateNative(_author, _post, busy, "ответ", _now.AddMinutes(3));
        var busyDeeper = Comment.CreateNative(_author, _post, busyReply, "глубже", _now.AddMinutes(4));
        var livelyReply = Comment.CreateNative(_author, _post, lively, "ответ", _now.AddMinutes(5));
        var tree = new[] { quiet, busy, busyReply, busyDeeper, lively, livelyReply }
            .OrderBy(c => c.Path, StringComparer.Ordinal)
            .ToArray();
        return (tree, quiet, busy, lively, busyReply, busyDeeper, livelyReply);
    }
}
