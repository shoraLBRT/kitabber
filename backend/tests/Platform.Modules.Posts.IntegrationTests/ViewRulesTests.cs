namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>E07-06: the rules of counting views (ADR-0071) — the day a viewer counts once in, the guest pseudonym, bots.</summary>
public sealed class ViewRulesTests
{
    private const string _browser =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36";

    [Theory]
    [InlineData("2026-11-26T00:00:00Z", "2026-11-26T00:00:00Z")]
    [InlineData("2026-11-26T23:59:59Z", "2026-11-26T00:00:00Z")]
    [InlineData("2026-11-27T02:30:00+03:00", "2026-11-26T00:00:00Z")]
    public void PeriodIsTheUtcDay(string now, string start) =>
        Assert.Equal(DateTimeOffset.Parse(start, null), ViewRules.PeriodOf(DateTimeOffset.Parse(now, null)));

    [Fact]
    public void GuestKeepsOnePseudonymUnderOneSecretAndAnotherUnderTheNext()
    {
        byte[] today = [.. Enumerable.Repeat((byte)1, 32)];
        byte[] tomorrow = [.. Enumerable.Repeat((byte)2, 32)];

        var first = ViewRules.GuestViewer(today, "203.0.113.7", _browser);

        Assert.Equal(first, ViewRules.GuestViewer(today, "203.0.113.7", _browser));
        Assert.NotEqual(first, ViewRules.GuestViewer(today, "203.0.113.8", _browser));
        Assert.NotEqual(first, ViewRules.GuestViewer(today, "203.0.113.7", _browser + " Edg/131.0"));
        Assert.NotEqual(first, ViewRules.GuestViewer(tomorrow, "203.0.113.7", _browser));
        Assert.StartsWith("g:", first, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.7", first, StringComparison.Ordinal);
        Assert.True(first.Length <= ViewRules.ViewerMaxLength);
        Assert.True(ViewRules.AccountViewer(Guid.NewGuid()).Length <= ViewRules.ViewerMaxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
    [InlineData("Mozilla/5.0 (compatible; YandexBot/3.0; +http://yandex.com/bots)")]
    [InlineData("Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)")]
    [InlineData("TelegramBot (like TwitterBot)")]
    [InlineData("facebookexternalhit/1.1")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/131.0 Safari/537.36")]
    [InlineData("curl/8.5.0")]
    [InlineData("python-requests/2.32")]
    [InlineData("Wget/1.21")]
    public void BotsAreNotCounted(string? userAgent) => Assert.True(ViewRules.IsBot(userAgent, purpose: null));

    [Theory]
    [InlineData(_browser)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 YaBrowser/24.12 Safari/537.36")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14.6; rv:133.0) Gecko/20100101 Firefox/133.0")]
    public void PeopleAreCounted(string userAgent) => Assert.False(ViewRules.IsBot(userAgent, purpose: null));

    [Fact]
    public void PrefetchIsNotCounted() => Assert.True(ViewRules.IsBot(_browser, "prefetch;prerender"));
}
