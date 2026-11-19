using Microsoft.Extensions.Time.Testing;

namespace Platform.Testing.Time;

/// <summary>
/// The managed clock of the tests (ADR-0008). Code takes the time from <see cref="TimeProvider"/> only; a test gives
/// it a <see cref="FakeTimeProvider"/> and moves it with <c>Advance</c> or <c>SetUtcNow</c>. The clock never moves
/// by itself.
/// </summary>
public static class TestClock
{
    /// <summary>Where every test clock starts: a fixed instant, so stored timestamps are the same on each run.</summary>
    public static readonly DateTimeOffset Start = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A new clock at <see cref="Start"/>, or at <paramref name="now"/>.</summary>
    public static FakeTimeProvider Create(DateTimeOffset? now = null) => new(now ?? Start);
}
