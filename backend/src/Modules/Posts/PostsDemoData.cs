using System.Text.Json.Nodes;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Identity.Contracts;
using Platform.Modules.Posts.Contracts;
using Platform.Seeding;

namespace Platform.Modules.Posts;

/// <summary>
/// The factory of posts (ADR-0009): the <c>number</c>-th post is a published native post of the demo account
/// <c>number % 20</c> in that account's personal blog, with a title and a body of paragraphs in the current block
/// schema (ADR-0023).
/// </summary>
public sealed class PostFactory(SeedData data) : ObjectFactory<Post>(data)
{
    /// <summary>The version of the block schema the demo bodies are written in.</summary>
    public const int BodyVersion = 1;

    private static readonly string[] _titles =
    [
        "Как я перестал бояться длинных текстов", "Пять книг на осень", "Заметки из поезда",
        "Почему старые карты интереснее новых", "Неделя без телефона", "Что я понял о кофе",
    ];

    private static readonly string[] _paragraphs =
    [
        "Сначала казалось, что это займёт пару вечеров.",
        "Потом выяснилось, что <b>самое интересное</b> — в деталях.",
        "Вот что получилось в итоге, коротко и по порядку.",
        "Если у вас был похожий опыт, расскажите в комментариях.",
    ];

    public override string Kind => PostsDemo.PostKind;

    /// <summary>A body of <paramref name="paragraphs"/> paragraph blocks, as JSON.</summary>
    public static string Body(params string[] paragraphs)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        var blocks = new JsonArray();
        for (var i = 0; i < paragraphs.Length; i++)
        {
            blocks.Add(new JsonObject
            {
                ["id"] = $"p{i}",
                ["type"] = "paragraph",
                ["data"] = new JsonObject { ["text"] = paragraphs[i] },
            });
        }

        return new JsonObject { ["version"] = BodyVersion, ["blocks"] = blocks }.ToJsonString();
    }

    protected override Post Create(int number)
    {
        var owner = number % IdentityDemo.AccountCount;
        var createdAt = Data.Now - TimeSpan.FromHours(number + 1);
        var post = Post.CreateDraft(
            IdOf(number),
            Data.Id(IdentityDemo.AccountKind, owner),
            Data.Id(BlogsDemo.BlogKind, owner),
            _titles[Random.Next(_titles.Length)],
            Body(_paragraphs[Random.Next(_paragraphs.Length)], _paragraphs[Random.Next(_paragraphs.Length)]),
            createdAt);
        post.Publish(createdAt + TimeSpan.FromMinutes(10));
        return post;
    }
}

/// <summary>The demo posts: two per demo account, added only where they are missing.</summary>
public sealed class PostsDemoSeeder(PostsDbContext db) : IDemoSeeder
{
    public const int Count = PostsDemo.Count;

    public string Name => PostsDbContext.Schema;

    public Task<int> SeedAsync(SeedData data, CancellationToken cancellationToken) =>
        db.AddMissingAsync(new PostFactory(data).BuildMany(Count).ToList(), p => p.Id, cancellationToken);
}
