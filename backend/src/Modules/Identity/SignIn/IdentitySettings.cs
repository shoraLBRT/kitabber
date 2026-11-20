using Platform.Configuration;
using Platform.Http.RateLimiting;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The settings of the Identity module in the API host (ADR-0002, ADR-0018); each one is described in docs/CONFIG.md.
/// </summary>
public static class IdentitySettings
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public const string DatabaseKey = "ConnectionStrings:" + ConnectionStringName;
    public const string IssuerKey = "Identity:Issuer";
    public const string SigningKeyKey = "Identity:SigningKey";
    public const string EncryptionKeyKey = "Identity:EncryptionKey";
    public const string SignInPathKey = "Identity:SignInPath";
    public const string WebRedirectUrisKey = "Identity:Clients:Web:RedirectUris";
    public const string AdminRedirectUrisKey = "Identity:Clients:Admin:RedirectUris";
    public const string SyncClientsKey = "Identity:SyncClients";
    public const string AuthorityKey = "Auth:Authority";
    public const string AudienceKey = "Auth:Audience";

    /// <summary>The audience of the platform's access tokens when <see cref="AudienceKey"/> is not set.</summary>
    public const string DefaultAudience = "platform-api";

    /// <summary>Where the provider sends a person who has not signed in yet (the sign-in window, E03).</summary>
    public const string DefaultSignInPath = "/login";

    /// <summary>
    /// Letters with an email confirmation code (ADR-0020): at most 5 an hour per account, so the platform cannot be
    /// used to flood someone else's mailbox. Overridden under <c>RateLimiting:Policies:identity.email-codes</c>.
    /// </summary>
    public static RateLimitPolicy EmailCodePolicy { get; } = new(5, TimeSpan.FromHours(1));

    /// <summary>
    /// Registrations and their new codes (E03-01): at most 10 an hour per network address, on top of the resend timer
    /// of each registration. Overridden under <c>RateLimiting:Policies:identity.registrations</c>.
    /// </summary>
    public static RateLimitPolicy RegistrationPolicy { get; } = new(10, TimeSpan.FromHours(1), RateLimitPartition.Ip);

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(DatabaseKey, "PostgreSQL of the platform: Host=...;Database=...;Username=...;Password=...")
        {
            Secret = true,
            RequiredWhen = Setting.Always,
        },
        new(IssuerKey, "public address of the OpenID Connect provider, the iss of every token, for example " +
            "https://example.org/; without it the address of the request"),
        new(SigningKeyKey, "RSA private key that signs tokens, PEM or base64 of DER (PKCS#8 or PKCS#1); required " +
            "outside Development and Test, where a key is generated at start")
        {
            Secret = true,
        },
        new(EncryptionKeyKey, "base64 of 32 random bytes (AES-256) that encrypt authorization codes; required outside " +
            "Development and Test, where a key is generated at start")
        {
            Secret = true,
        },
        new(SignInPathKey, "path of the sign-in window the provider sends a person to before it issues a code")
        {
            Default = DefaultSignInPath,
        },
        new(WebRedirectUrisKey, "redirect addresses of the web client (Nuxt), comma-separated absolute URIs, for " +
            "example https://example.org/auth/callback; without them the client is not registered"),
        new(AdminRedirectUrisKey, "redirect addresses of the admin panel, comma-separated absolute URIs; without them " +
            "the client is not registered"),
        new(SyncClientsKey, "false: the API does not write the clients into the database at start; for the OpenAPI " +
            "build and tests, which have no database or migrate it after the start")
        {
            Default = "true",
            AllowedValues = ["true", "false"],
        },
        new(AuthorityKey, "issuer whose access tokens the API accepts; empty for this API's own provider, an external " +
            "address (https://accounts.example.org/) switches the API to another OpenID Connect provider"),
        new(AudienceKey, "audience an access token must carry for the API")
        {
            Default = DefaultAudience,
        },
        .. RateLimitingSettings.Policy(Email.AccountEmailEndpoints.CodePolicy, EmailCodePolicy, "letters with an email confirmation code"),
        .. RateLimitingSettings.Policy(Registration.RegistrationEndpoints.Policy, RegistrationPolicy, "registrations and their new codes"),
    ];
}
