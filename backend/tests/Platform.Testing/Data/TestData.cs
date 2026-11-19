using Platform.Seeding;
using Platform.Testing.Time;

namespace Platform.Testing.Data;

/// <summary>
/// Test data from the modules' factories (ADR-0009). A test builds its objects with the factory of the module, on a
/// <see cref="SeedData"/> from here: the fixed seed and the test clock, so identifiers, texts and timestamps are the
/// same on every run. Only the values the test checks are set by hand, with <c>Build(x =&gt; ...)</c>.
/// </summary>
public static class TestData
{
    /// <summary>Seed data on <paramref name="time"/> (the factory's clock in an API test) or a new test clock.</summary>
    public static SeedData Create(TimeProvider? time = null, int seed = SeedData.DefaultSeed) =>
        new(seed, time ?? TestClock.Create());
}
