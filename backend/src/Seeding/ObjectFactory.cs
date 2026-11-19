namespace Platform.Seeding;

/// <summary>
/// Builds valid objects of one kind (ADR-0009). A module writes one factory per entity next to the entity; its demo
/// seeder and its tests use the same factory. Objects are numbered from 0; the <paramref name="number"/>-th object
/// gets <see cref="SeedData.Id"/>(<see cref="Kind"/>, number) as its identifier, so a rebuilt object is the same
/// object. Building does not touch the database.
/// </summary>
public abstract class ObjectFactory<T>(SeedData data)
    where T : class
{
    private int _next;
    private Random? _random;

    protected SeedData Data { get; } = data ?? throw new ArgumentNullException(nameof(data));

    /// <summary>The kind of the objects, such as <c>account</c>: it scopes their identifiers and random values.</summary>
    public abstract string Kind { get; }

    /// <summary>The random sequence of this factory: the same for every factory of this kind and seed.</summary>
    protected Random Random => _random ??= Data.Random(Kind);

    /// <summary>The identifier of the <paramref name="number"/>-th object of this kind.</summary>
    public Guid IdOf(int number) => Data.Id(Kind, number);

    /// <summary>The next object, optionally changed by <paramref name="customize"/> (for the case a test checks).</summary>
    public T Build(Action<T>? customize = null)
    {
        var item = Create(_next++);
        customize?.Invoke(item);
        return item;
    }

    /// <summary>The next <paramref name="count"/> objects; <paramref name="customize"/> gets each with its index.</summary>
    public IReadOnlyList<T> BuildMany(int count, Action<T, int>? customize = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var items = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            var item = Create(_next++);
            customize?.Invoke(item, i);
            items.Add(item);
        }

        return items;
    }

    /// <summary>Creates the <paramref name="number"/>-th object: valid, with every required value set.</summary>
    protected abstract T Create(int number);
}
