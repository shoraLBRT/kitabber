using Microsoft.Extensions.Configuration;
using Platform.Testing.Time;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>E09-01: the comment model, its materialized path, the depth limit and guest comments. No database.</summary>
public sealed class CommentRulesTests
{
    private static readonly DateTimeOffset _now = TestClock.Create().GetUtcNow();
    private static readonly Guid _post = Guid.CreateVersion7(_now);
    private static readonly Guid _author = Guid.CreateVersion7(_now.AddSeconds(1));

    [Fact]
    public void ReplyToThePostIsTheRootOfItsBranch()
    {
        var comment = Comment.CreateNative(_author, _post, parent: null, "  Привет  ", _now);

        Assert.Null(comment.ParentId);
        Assert.Equal(1, comment.Depth);
        Assert.Equal(comment.Id.ToString("N"), comment.Path);
        Assert.Equal("Привет", comment.Text);
        Assert.Equal(CommentKind.Native, comment.Kind);
        Assert.Equal(_author, comment.AuthorAccountId);
        Assert.Null(comment.Guest);
    }

    [Fact]
    public void ReplyExtendsThePathOfItsParent()
    {
        var root = Comment.CreateNative(_author, _post, parent: null, "Первый", _now);
        var reply = Comment.CreateNative(_author, _post, root, "Ответ", _now.AddMinutes(1));

        Assert.Equal(root.Id, reply.ParentId);
        Assert.Equal(2, reply.Depth);
        Assert.Equal($"{root.Path}.{reply.Id:N}", reply.Path);
    }

    [Fact]
    public void PathOrderIsTheOrderOfTheTree()
    {
        var first = Comment.CreateNative(_author, _post, parent: null, "1", _now);
        var second = Comment.CreateNative(_author, _post, parent: null, "2", _now.AddMinutes(1));
        var replyToFirst = Comment.CreateNative(_author, _post, first, "1.1", _now.AddMinutes(2));

        var ordered = new[] { second, replyToFirst, first }.OrderBy(c => c.Path, StringComparer.Ordinal).ToList();

        Assert.Equal([first, replyToFirst, second], ordered);
    }

    [Fact]
    public void ReplyIsUnderThePostOfItsParent()
    {
        var root = Comment.CreateNative(_author, _post, parent: null, "Первый", _now);

        Assert.Throws<ArgumentException>(() =>
            Comment.CreateNative(_author, Guid.CreateVersion7(_now.AddDays(1)), root, "Ответ", _now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    public void CommentNeedsText(string text) =>
        Assert.Throws<ArgumentException>(() => Comment.CreateNative(_author, _post, parent: null, text, _now));

    [Fact]
    public void NativeCommentNeedsAnAuthorAccount() =>
        Assert.Throws<ArgumentException>(() => Comment.CreateNative(Guid.Empty, _post, parent: null, "Текст", _now));

    [Fact]
    public void GuestCommentNeedsNoAccount()
    {
        var guest = Comment.CreateGuest(
            _post, parent: null, new GuestAuthor(" Иван ", new Uri("https://t.me/ivan")),
            new CommentSource("-100123", "42"), "Из Telegram", _now);
        var reply = Comment.CreateGuest(
            _post, guest, new GuestAuthor("Пётр", Url: null), new CommentSource("-100123", "43"), "Ответ", _now);

        Assert.Equal(CommentKind.Guest, guest.Kind);
        Assert.Null(guest.AuthorAccountId);
        Assert.Equal(new GuestAuthor("Иван", new Uri("https://t.me/ivan")), guest.Guest);
        Assert.Equal("-100123", guest.SourceChannel);
        Assert.Equal(2, reply.Depth);
        Assert.Equal(guest.Id, reply.ParentId);
    }

    [Fact]
    public void NoOneRepliesToAGuestComment()
    {
        var guest = Comment.CreateGuest(
            _post, parent: null, new GuestAuthor("Иван", Url: null), new CommentSource("-100123", "42"), "Текст", _now);

        Assert.NotNull(CommentRules.ReplyProblem(guest, maxDepth: 8));
    }

    [Fact]
    public void ReplyDeeperThanTheLimitIsRefused()
    {
        var level1 = Comment.CreateNative(_author, _post, parent: null, "1", _now);
        var level2 = Comment.CreateNative(_author, _post, level1, "2", _now);

        Assert.Null(CommentRules.ReplyProblem(level1, maxDepth: 2));
        Assert.NotNull(CommentRules.ReplyProblem(level2, maxDepth: 2));
    }

    [Theory]
    [InlineData(null, 8)]
    [InlineData("3", 3)]
    [InlineData("32", 32)]
    public void MaxDepthComesFromTheSetting(string? value, int expected) =>
        Assert.Equal(expected, CommentsSettings.ReadLimits(Configuration(value)).MaxDepth);

    [Theory]
    [InlineData("0")]
    [InlineData("33")]
    [InlineData("-1")]
    [InlineData("deep")]
    public void MaxDepthOutsideItsRangeStopsTheHost(string value) =>
        Assert.Throws<InvalidOperationException>(() => CommentsSettings.ReadLimits(Configuration(value)));

    private static IConfiguration Configuration(string? maxDepth) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [CommentsSettings.MaxDepthKey] = maxDepth })
        .Build();
}
