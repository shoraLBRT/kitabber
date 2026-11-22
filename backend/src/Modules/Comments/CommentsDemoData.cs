using Platform.Modules.Identity.Contracts;
using Platform.Modules.Posts.Contracts;
using Platform.Seeding;

namespace Platform.Modules.Comments;

/// <summary>
/// The factory of comments (ADR-0009): the <c>number</c>-th comment is a native comment of the demo account
/// <c>number % 20</c> under the demo post <c>number / CommentsPerPost</c>. In each post the first two comments reply to
/// the post, and every next one replies to the comment two before it, so the demo trees have branches three levels deep.
/// </summary>
public sealed class CommentFactory(SeedData data) : ObjectFactory<Comment>(data)
{
    /// <summary>How many comments each commented demo post has.</summary>
    public const int CommentsPerPost = 6;

    private static readonly string[] _texts =
    [
        "Спасибо, очень по делу.", "А можно подробнее про второй пункт?", "Согласен не со всем, но читать было интересно.",
        "У меня было ровно так же.", "Сохранил, перечитаю вечером.", "Хороший вопрос — сам думал об этом.",
    ];

    private readonly Dictionary<int, Comment> _built = [];

    public override string Kind => "comment";

    protected override Comment Create(int number)
    {
        var post = number / CommentsPerPost;
        var place = number % CommentsPerPost;
        var parent = place >= 2 && _built.TryGetValue(number - 2, out var older) ? older : null;
        var comment = Comment.CreateNative(
            IdOf(number),
            Data.Id(IdentityDemo.AccountKind, number % IdentityDemo.AccountCount),
            Data.Id(PostsDemo.PostKind, post),
            parent,
            _texts[Random.Next(_texts.Length)],
            Data.Now - TimeSpan.FromHours(post + 1) + TimeSpan.FromMinutes(20 + place));
        _built[number] = comment;
        return comment;
    }
}

/// <summary>The demo comments: a small tree under each of the first ten demo posts, added only where they are missing.</summary>
public sealed class CommentsDemoSeeder(CommentsDbContext db) : IDemoSeeder
{
    /// <summary>How many demo posts get comments.</summary>
    public const int CommentedPosts = 10;

    public const int Count = CommentedPosts * CommentFactory.CommentsPerPost;

    public string Name => CommentsDbContext.Schema;

    public Task<int> SeedAsync(SeedData data, CancellationToken cancellationToken) =>
        db.AddMissingAsync(new CommentFactory(data).BuildMany(Count).ToList(), c => c.Id, cancellationToken);
}
