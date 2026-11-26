using Platform.Modules.Identity.SignIn;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>The name a device gets in the list of devices (ADR-0078), without a database.</summary>
public sealed class DeviceNamesTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36", "Chrome, Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1", "Safari, iOS")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:133.0) Gecko/20100101 Firefox/133.0", "Firefox, Linux")]
    [InlineData("Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Mobile Safari/537.36 Edg/131.0", "Edge, Android")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15", "Safari, macOS")]
    [InlineData("curl/8.4.0", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void DeviceIsNamedByItsBrowserAndSystem(string? userAgent, string? expected) =>
        Assert.Equal(expected, DeviceNames.Describe(userAgent));
}
