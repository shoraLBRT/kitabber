using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Testing.Api;
using Platform.Testing.Time;

namespace Platform.Api.Tests;

/// <summary>Time comes from TimeProvider: the system clock in the host, the managed clock in tests (ADR-0008).</summary>
public sealed class TestClockTests
{
    [Fact]
    public void ApiHostUsesTheSystemClock()
    {
        using var services = new ServiceCollection().AddPlatformApi().BuildServiceProvider();

        Assert.Same(TimeProvider.System, services.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void FactoryReplacesTheClockWithTheTestClock()
    {
        using var factory = new PlatformApiFactory();

        var clock = factory.Services.GetRequiredService<TimeProvider>();

        Assert.Same(factory.Time, clock);
        Assert.Equal(TestClock.Start, clock.GetUtcNow());
    }

    [Fact]
    public void TestClockMovesOnlyWhenTold()
    {
        var clock = TestClock.Create();
        var before = clock.GetUtcNow();

        clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(TestClock.Start, before);
        Assert.Equal(TestClock.Start.AddHours(1), clock.GetUtcNow());
        Assert.Equal(TestClock.Start.AddHours(1), clock.GetUtcNow());
    }

    [Fact]
    public void SettingsReachTheHostConfiguration()
    {
        using var factory = new PlatformApiFactory().WithSetting("Sample:Value", "from-test");

        Assert.Equal("from-test", factory.Services.GetRequiredService<IConfiguration>()["Sample:Value"]);
    }
}
