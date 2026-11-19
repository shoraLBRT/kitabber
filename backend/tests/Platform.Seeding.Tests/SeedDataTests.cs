using Platform.Configuration;
using Platform.Testing.Data;
using Platform.Testing.Time;

namespace Platform.Seeding.Tests;

/// <summary>The rules of generated data: one seed and one clock give the same values; scopes do not interfere.</summary>
public sealed class SeedDataTests
{
    [Fact]
    public void SameSeedGivesSameIdentifiersAndRandomValues()
    {
        var first = TestData.Create();
        var second = TestData.Create();

        Assert.Equal(first.Id("account", 3), second.Id("account", 3));
        Assert.Equal(Draw(first.Random("post")), Draw(second.Random("post")));
    }

    [Fact]
    public void IdentifiersDifferByKindNumberAndSeed()
    {
        var data = TestData.Create();
        var other = TestData.Create(seed: 7);

        Guid[] ids = [data.Id("account", 0), data.Id("account", 1), data.Id("post", 0), other.Id("account", 0)];

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Equal(8, id.Version));
    }

    [Fact]
    public void RandomSequencesOfScopesAreIndependent()
    {
        var data = TestData.Create();
        var alone = Draw(data.Random("post"));

        var other = data.Random("account");
        _ = Draw(other);
        var afterOther = Draw(data.Random("post"));

        Assert.Equal(alone, afterOther);
        Assert.NotEqual(alone, Draw(data.Random("account")));
    }

    [Fact]
    public void FactoryNumbersObjectsAndTakesTimeFromTheClock()
    {
        var clock = TestClock.Create();
        var factory = new ItemFactory(TestData.Create(clock));

        var first = factory.Build();
        var rest = factory.BuildMany(2, (item, i) => item.Title = $"custom {i}");
        clock.Advance(TimeSpan.FromHours(1));
        var late = factory.Build(item => item.Title = "late");

        Assert.Equal(factory.IdOf(0), first.Id);
        Assert.Equal([factory.IdOf(1), factory.IdOf(2)], rest.Select(i => i.Id));
        Assert.Equal(["custom 0", "custom 1"], rest.Select(i => i.Title));
        Assert.Equal(TestClock.Start, first.CreatedAt);
        Assert.Equal(TestClock.Start.AddHours(1), late.CreatedAt);
        Assert.Equal("late", late.Title);
    }

    [Fact]
    public void FactoryBuildsTheSameObjectsForTheSameSeed()
    {
        var first = new ItemFactory(TestData.Create()).BuildMany(5);
        var second = new ItemFactory(TestData.Create()).BuildMany(5);
        var otherSeed = new ItemFactory(TestData.Create(seed: 1)).BuildMany(5);

        Assert.Equal(first, second);
        Assert.NotEqual(first.Select(i => i.Id), otherSeed.Select(i => i.Id));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task SeedCommandRefusesProtectedEnvironments(string environment)
    {
        var (exit, error) = await RunSeed("--environment", environment, Connection);

        Assert.Equal(SeedApp.RefusedExitCode, exit);
        Assert.Contains($"--{SeedApp.AllowProtectedEnvironmentKey}=true", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--Seed:RandomSeed=abc")]
    [InlineData("--Seed:AllowProtectedEnvironment=maybe")]
    public async Task SeedCommandRejectsWrongSettings(string setting)
    {
        var (exit, _) = await RunSeed("--environment", PlatformEnvironments.Test, Connection, setting);

        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task SeedCommandRejectsUnknownCommand()
    {
        var (exit, error) = await RunSeed("drop", "--environment", PlatformEnvironments.Test, Connection);

        Assert.Equal(2, exit);
        Assert.Contains(SeedApp.Usage, error, StringComparison.Ordinal);
    }

    private const string Connection = "--ConnectionStrings:Platform=Host=localhost;Database=never_contacted";

    private static async Task<(int Exit, string Error)> RunSeed(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await SeedApp.RunAsync(args, output, error, (_, _) => { });
        return (exit, error.ToString());
    }

#pragma warning disable CA5394 // The test reads the repeatable sequence.
    private static int[] Draw(Random random) => [.. Enumerable.Range(0, 5).Select(_ => random.Next())];
#pragma warning restore CA5394

    private sealed record Item
    {
        public Guid Id { get; init; }

        public string Title { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }
    }

    private sealed class ItemFactory(SeedData data) : ObjectFactory<Item>(data)
    {
        public override string Kind => "item";

#pragma warning disable CA5394 // Demo data needs a repeatable sequence.
        protected override Item Create(int number) =>
            new() { Id = IdOf(number), Title = $"item {Random.Next(1000)}", CreatedAt = Data.Now };
#pragma warning restore CA5394
    }
}
