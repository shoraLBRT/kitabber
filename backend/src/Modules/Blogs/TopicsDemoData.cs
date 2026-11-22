using Platform.Modules.Blogs.Contracts;
using Platform.Seeding;

namespace Platform.Modules.Blogs;

/// <summary>
/// The demo topics (ADR-0046): a fixed short list, so the editor has topics to choose from before topics get their
/// own model and administration (E11-01, #76). The <c>number</c>-th topic (from 0) is <c>Id("topic", number)</c>.
/// </summary>
public sealed class TopicFactory(SeedData data) : ObjectFactory<Subsite>(data)
{
    /// <summary>The demo topics' names and addresses, in seeding order.</summary>
    public static readonly IReadOnlyList<(string Name, string Nickname)> Topics =
    [
        ("Кино и сериалы", "kino"), ("Игры", "games"), ("Технологии", "tech"),
        ("Книги", "books"), ("Наука", "science"), ("Путешествия", "travel"),
    ];

    public override string Kind => BlogsDemo.TopicKind;

    protected override Subsite Create(int number)
    {
        var (name, nickname) = Topics[number % Topics.Count];
        var topic = new Subsite
        {
            Id = IdOf(number),
            Type = SubsiteType.Topic,
            CreatedAt = Data.Now - TimeSpan.FromDays(100 + number),
        };
        topic.Rename(name);
        topic.SetNickname(number < Topics.Count ? nickname : $"{nickname}{number}");
        return topic;
    }
}
