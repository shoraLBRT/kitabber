using Microsoft.Extensions.Configuration;
using Platform.Testing.Time;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>
/// E09-05: the author changes a comment's text while the time to change it lasts and deletes it; a deleted comment with
/// replies stays as their stub, one without them goes with every stub left empty above it. No database.
/// </summary>
public sealed class CommentChangeRulesTests
{
    private static readonly DateTimeOffset _now = TestClock.Create().GetUtcNow();
    private static readonly Guid _post = Guid.CreateVersion7(_now);
    private static readonly Guid _author = Guid.CreateVersion7(_now.AddSeconds(1));
    private static readonly Guid _other = Guid.CreateVersion7(_now.AddSeconds(2));
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(15);

    [Fact]
    public void AuthorChangesTheTextUntilTheTimeIsOver()
    {
        var comment = Comment.CreateNative(_author, _post, parent: null, "Было", _now);

        Assert.Equal(_now + _window, CommentRules.EditableUntil(comment, _window));
        Assert.Equal(CommentChange.Allowed, CommentRules.ChangeProblem(comment, _author, edit: true, _now, _window));
        Assert.Equal(
            CommentChange.Allowed,
            CommentRules.ChangeProblem(comment, _author, edit: true, _now + _window - TimeSpan.FromSeconds(1), _window));
        Assert.Equal(
            CommentChange.EditClosed, CommentRules.ChangeProblem(comment, _author, edit: true, _now + _window, _window));
    }

    [Fact]
    public void AuthorDeletesAtAnyTime()
    {
        var comment = Comment.CreateNative(_author, _post, parent: null, "Текст", _now);

        Assert.Equal(
            CommentChange.Allowed, CommentRules.ChangeProblem(comment, _author, edit: false, _now.AddDays(30), _window));
    }

    [Fact]
    public void OnlyTheAuthorChangesOrDeletes()
    {
        var comment = Comment.CreateNative(_author, _post, parent: null, "Текст", _now);
        var guest = Comment.CreateGuest(
            _post, parent: null, new GuestAuthor("Иван", Url: null), new CommentSource("-100", "1"), "Текст", _now);

        Assert.Equal(CommentChange.NotAuthor, CommentRules.ChangeProblem(comment, _other, edit: true, _now, _window));
        Assert.Equal(CommentChange.NotAuthor, CommentRules.ChangeProblem(comment, _other, edit: false, _now, _window));
        Assert.Equal(CommentChange.NotAuthor, CommentRules.ChangeProblem(guest, _author, edit: false, _now, _window));
        Assert.Null(CommentRules.EditableUntil(guest, _window));
    }

    [Fact]
    public void EditChangesTheTextAndMarksIt()
    {
        var comment = Comment.CreateNative(_author, _post, parent: null, "Было", _now);

        comment.Edit("  Стало  ", _now.AddMinutes(3));

        Assert.Equal("Стало", comment.Text);
        Assert.Equal(_now.AddMinutes(3), comment.EditedAt);
        Assert.Throws<ArgumentException>(() => comment.Edit("  ", _now.AddMinutes(4)));
    }

    [Fact]
    public void DeletedCommentWithRepliesStaysAsTheirStub()
    {
        var root = Comment.CreateNative(_author, _post, parent: null, "Корень", _now);
        var reply = Comment.CreateNative(_other, _post, root, "Ответ", _now.AddMinutes(1));

        var removed = CommentTree.Delete([root, reply], root.Id, _now.AddMinutes(2));

        Assert.Empty(removed);
        Assert.True(root.IsDeleted);
        Assert.Equal(string.Empty, root.Text);
        Assert.Null(CommentRules.EditableUntil(root, _window));
        Assert.Equal(CommentChange.NotFound, CommentRules.ChangeProblem(root, _author, edit: false, _now, _window));
        Assert.NotNull(CommentRules.ReplyProblem(root, maxDepth: 8));
        Assert.Throws<InvalidOperationException>(() => root.Edit("Снова", _now));
    }

    [Fact]
    public void DeletedCommentWithoutRepliesGoesWithTheStubsLeftEmptyAboveIt()
    {
        var root = Comment.CreateNative(_author, _post, parent: null, "Корень", _now);
        var middle = Comment.CreateNative(_author, _post, root, "Середина", _now.AddMinutes(1));
        var leaf = Comment.CreateNative(_other, _post, middle, "Лист", _now.AddMinutes(2));
        var sibling = Comment.CreateNative(_other, _post, root, "Сосед", _now.AddMinutes(3));
        List<Comment> thread = [root, middle, leaf, sibling];
        CommentTree.Delete(thread, root.Id, _now);
        CommentTree.Delete(thread, middle.Id, _now);

        var removed = CommentTree.Delete(thread, leaf.Id, _now);

        // The middle stub has nothing left under it; the root still holds the sibling's place.
        Assert.Equal([leaf, middle], removed);
        Assert.True(root.IsDeleted);
        var lone = Comment.CreateNative(_author, _post, parent: null, "Один", _now);
        Assert.Equal([lone], CommentTree.Delete([lone], lone.Id, _now));
    }

    [Theory]
    [InlineData(null, 15)]
    [InlineData("1", 1)]
    [InlineData("1440", 1440)]
    public void EditTimeComesFromTheSetting(string? value, int expected) =>
        Assert.Equal(expected, CommentsSettings.ReadLimits(Configuration(value)).EditMinutes);

    [Theory]
    [InlineData("0")]
    [InlineData("1441")]
    [InlineData("soon")]
    public void EditTimeOutsideItsRangeStopsTheHost(string value) =>
        Assert.Throws<InvalidOperationException>(() => CommentsSettings.ReadLimits(Configuration(value)));

    private static IConfiguration Configuration(string? minutes) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [CommentsSettings.EditMinutesKey] = minutes })
        .Build();
}
