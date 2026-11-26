namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// One device signed in to the account (E03-09, ADR-0078): the person's session at the provider. Every way of signing
/// in starts one; its id is the <c>sid</c> claim of the provider's cookie and of every token issued through it, so
/// ending it (deleting the row) stops the cookie, the refresh tokens and, within a minute, the access tokens.
/// </summary>
public sealed class AccountSession
{
    public const int DeviceMaxLength = 128;

    private AccountSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    /// <summary>
    /// What the device is, read from its browser at sign-in: «Chrome, Windows»; null when the browser says nothing
    /// known. Neither the address nor the raw header is kept.
    /// </summary>
    public string? Device { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the device last signed in or refreshed its tokens: every few minutes while the site is open.</summary>
    public DateTimeOffset LastActiveAt { get; private set; }

    public static AccountSession Start(Guid accountId, string? userAgent, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        AccountId = accountId,
        Device = DeviceNames.Describe(userAgent),
        CreatedAt = now,
        LastActiveAt = now,
    };
}

/// <summary>A short name of a device from its <c>User-Agent</c>: the browser and the system, in that order.</summary>
public static class DeviceNames
{
    // Order matters: Edge and Opera say «Chrome» too, Chrome says «Safari», Android says «Linux», iPadOS «Mac OS X».
    private static readonly (string Token, string Name)[] _browsers =
    [
        ("Edg/", "Edge"), ("OPR/", "Opera"), ("YaBrowser/", "Яндекс Браузер"), ("SamsungBrowser/", "Samsung Internet"),
        ("Firefox/", "Firefox"), ("FxiOS/", "Firefox"), ("CriOS/", "Chrome"), ("Chrome/", "Chrome"), ("Safari/", "Safari"),
    ];

    private static readonly (string Token, string Name)[] _systems =
    [
        ("Windows", "Windows"), ("Android", "Android"), ("iPhone", "iOS"), ("iPad", "iPadOS"), ("CrOS", "ChromeOS"),
        ("Mac OS X", "macOS"), ("Macintosh", "macOS"), ("Linux", "Linux"),
    ];

    public static string? Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var browser = First(_browsers, userAgent);
        var system = First(_systems, userAgent);
        return (browser, system) switch
        {
            (null, _) => system,
            (_, null) => browser,
            _ => $"{browser}, {system}",
        };

        static string? First((string Token, string Name)[] known, string value) =>
            known.FirstOrDefault(k => value.Contains(k.Token, StringComparison.Ordinal)).Name;
    }
}
