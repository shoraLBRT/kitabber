using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Identity.Contracts;
using Platform.Seeding;

namespace Platform.Modules.Blogs;

/// <summary>
/// The factory of personal blogs (ADR-0009): the <c>number</c>-th blog belongs to the <c>number</c>-th demo account,
/// <c>Id("account", number)</c>, and has a name, a nickname and a description within the rules.
/// </summary>
public sealed class PersonalBlogFactory(SeedData data) : ObjectFactory<Subsite>(data)
{
    private static readonly string[] _names =
    [
        "Заметки на полях", "Кофе и код", "Северный ветер", "Книжная полка", "Город после дождя",
        "Тихий час", "Полевые записи", "Чердак", "Две строки", "Медленное чтение",
    ];

    private static readonly string[] _descriptions =
    [
        "Пишу о книгах, которые стоит перечитать.",
        "Про работу, путешествия и всё, что между ними.",
        "Короткие тексты раз в неделю.",
        "Фотографии и истории из маленьких городов.",
        "Разбираю, как устроены привычные вещи.",
    ];

    public override string Kind => BlogsDemo.BlogKind;

    protected override Subsite Create(int number)
    {
        var owner = Data.Id(IdentityDemo.AccountKind, number);
        var blog = new Subsite
        {
            Id = IdOf(number),
            Type = SubsiteType.Personal,
            OwnerAccountId = owner,
            CreatedAt = Data.Now - TimeSpan.FromDays(number + 1) + TimeSpan.FromHours(1),
        };
        blog.Rename(_names[Random.Next(_names.Length)]);
        blog.Describe(_descriptions[Random.Next(_descriptions.Length)]);
        blog.SetNickname($"demo{number}");
        return blog;
    }
}

/// <summary>The personal blogs of the demo accounts and the demo topics, added only where they are missing.</summary>
public sealed class BlogsDemoSeeder(BlogsDbContext db) : IDemoSeeder
{
    public string Name => BlogsDbContext.Schema;

    public Task<int> SeedAsync(SeedData data, CancellationToken cancellationToken) =>
        db.AddMissingAsync(
            [
                .. new PersonalBlogFactory(data).BuildMany(IdentityDemo.AccountCount),
                .. new TopicFactory(data).BuildMany(TopicFactory.Topics.Count),
            ],
            s => s.Id,
            cancellationToken);
}
