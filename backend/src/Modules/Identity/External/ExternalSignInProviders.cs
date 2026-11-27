using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Configuration;
using Platform.Modules.Identity.SignIn;

namespace Platform.Modules.Identity.External;

/// <summary>
/// Who the provider says the person is (ADR-0115): the subject, and the email when the provider gives one.
/// <paramref name="EmailVerified"/> is the provider's word that the address is the person's; only then does the email
/// find or create an account.
/// </summary>
public sealed record ExternalProfile(string Subject, string? Email, bool EmailVerified);

/// <summary>
/// One sign-in at a provider in progress: the <paramref name="State"/> the provider must send back, the PKCE
/// <paramref name="CodeVerifier"/>, the API's <paramref name="Callback"/> address and the <paramref name="SiteOrigin"/> the
/// person came from (Telegram returns to a page of the site, not to the API).
/// </summary>
public sealed record ExternalChallenge(string State, string CodeVerifier, Uri Callback, Uri SiteOrigin);

/// <summary>
/// An external sign-in provider (FR-AUTH, ADR-0115), like every external service behind an interface with a fake
/// (<see cref="FakeExternalProvider"/>). The provider builds the address the browser goes to and, once the browser is
/// back, proves who the person is from the parameters it came back with.
/// </summary>
public interface IExternalSignInProvider
{
    /// <summary>The provider's key, <see cref="ExternalProviders"/>.</summary>
    string Key { get; }

    /// <summary>Whether the provider sends the <see cref="ExternalChallenge.State"/> back (Telegram does not).</summary>
    bool ReturnsState { get; }

    /// <summary>Where the browser goes to sign in at the provider.</summary>
    Uri AuthorizationUri(ExternalChallenge challenge);

    /// <summary>The person the callback parameters prove; null when they prove nobody or the provider refused.</summary>
    Task<ExternalProfile?> CompleteAsync(
        IReadOnlyDictionary<string, string> callback, ExternalChallenge challenge, CancellationToken cancellationToken);
}

/// <summary>The providers turned on by the settings (ADR-0115): a provider without its keys is not here.</summary>
public sealed class ExternalSignInProviders(IReadOnlyList<IExternalSignInProvider> providers, bool fake)
{
    /// <summary>Whether the providers are the fakes of <see cref="ExternalSignInSettings.FakeKey"/>.</summary>
    public bool IsFake { get; } = fake;

    /// <summary>The keys of the providers turned on, in the order of FR-AUTH.</summary>
    public IReadOnlyList<string> Keys { get; } = [.. providers.Select(p => p.Key)];

    /// <summary>The provider of <paramref name="key"/> when it is turned on.</summary>
    public IExternalSignInProvider? Find(string? key) =>
        providers.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));
}

/// <summary>The settings of the external sign-in providers (ADR-0115).</summary>
public static partial class ExternalSignInSettings
{
    public const string FakeKey = "Identity:External:Fake";
    public const string YandexClientIdKey = "Identity:External:Yandex:ClientId";
    public const string YandexClientSecretKey = "Identity:External:Yandex:ClientSecret";
    public const string VkClientIdKey = "Identity:External:Vk:ClientId";
    public const string GoogleClientIdKey = "Identity:External:Google:ClientId";
    public const string GoogleClientSecretKey = "Identity:External:Google:ClientSecret";
    public const string TelegramBotTokenKey = "Identity:External:Telegram:BotToken";

    /// <summary>The name of the HTTP client the providers talk to their servers with.</summary>
    public const string HttpClientName = "identity.external";

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(FakeKey, "true: every external sign-in provider is a fake that signs in whoever the form names; refused " +
            "outside Development and Test")
        {
            Default = "false",
            AllowedValues = ["true", "false"],
        },
        new(YandexClientIdKey, "client id of the Yandex ID application; with its secret it turns on the sign-in with Yandex"),
        new(YandexClientSecretKey, "client secret of the Yandex ID application")
        {
            Secret = true,
        },
        new(VkClientIdKey, "app id of the VK ID application; it turns on the sign-in with VK"),
        new(GoogleClientIdKey, "OAuth client id of the Google Cloud project; with its secret it turns on the sign-in with Google"),
        new(GoogleClientSecretKey, "OAuth client secret of the Google Cloud project")
        {
            Secret = true,
        },
        new(TelegramBotTokenKey, "token of the bot the Telegram Login goes through (123456:ABC...); it turns on the " +
            "sign-in with Telegram")
        {
            Secret = true,
        },
    ];

    /// <summary>The providers of the configuration; a half-configured provider stops the start.</summary>
    public static IServiceCollection AddExternalSignIn(
        this IServiceCollection services, IConfiguration configuration, string environment)
    {
        services.AddHttpClient(HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        var fake = string.Equals(configuration[FakeKey], "true", StringComparison.OrdinalIgnoreCase);
        if (fake && !ProviderKeys.MayGenerateKeys(environment))
        {
            throw new InvalidConfigurationException(
                [$"{FakeKey} = true is allowed only in Development and Test, not in {environment}"]);
        }

        List<string> problems = [];
        var yandex = Pair(configuration, YandexClientIdKey, YandexClientSecretKey, problems);
        var vk = Value(configuration, VkClientIdKey);
        var google = Pair(configuration, GoogleClientIdKey, GoogleClientSecretKey, problems);
        var bot = Value(configuration, TelegramBotTokenKey);
        if (bot is not null && !BotTokenPattern().IsMatch(bot))
        {
            problems.Add($"{TelegramBotTokenKey}: not a bot token of the form 123456:ABC...");
        }

        if (problems.Count > 0)
        {
            throw new InvalidConfigurationException(problems);
        }

        services.AddSingleton(provider =>
        {
            if (fake)
            {
                return new ExternalSignInProviders(
                    [.. new[] { ExternalProviders.Yandex, ExternalProviders.Vk, ExternalProviders.Google, ExternalProviders.Telegram }
                        .Select(key => (IExternalSignInProvider)new FakeExternalProvider(key))],
                    fake: true);
            }

            var http = provider.GetRequiredService<IHttpClientFactory>();
            List<IExternalSignInProvider> list = [];
            if (yandex is { } y)
            {
                list.Add(new YandexProvider(http, y.Id, y.Secret));
            }

            if (vk is not null)
            {
                list.Add(new VkProvider(http, vk));
            }

            if (google is { } g)
            {
                list.Add(new GoogleProvider(http, g.Id, g.Secret));
            }

            if (bot is not null)
            {
                list.Add(new TelegramLoginProvider(bot, provider.GetRequiredService<TimeProvider>()));
            }

            return new ExternalSignInProviders(list, fake: false);
        });
        return services;
    }

    private static string? Value(IConfiguration configuration, string key) =>
        configuration[key] is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static (string Id, string Secret)? Pair(IConfiguration configuration, string idKey, string secretKey, List<string> problems)
    {
        var id = Value(configuration, idKey);
        var secret = Value(configuration, secretKey);
        if (id is null && secret is null)
        {
            return null;
        }

        if (id is null || secret is null)
        {
            problems.Add($"{idKey} and {secretKey}: set both or neither");
            return null;
        }

        return (id, secret);
    }

    [GeneratedRegex(@"^\d+:[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex BotTokenPattern();
}
