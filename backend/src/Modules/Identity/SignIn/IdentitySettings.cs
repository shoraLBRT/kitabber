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
    public const string CaptchaProviderKey = "Identity:Captcha:Provider";

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

    /// <summary>
    /// Password attempts and entries of a sign-in code (E03-02): at most 30 in 15 minutes per network address.
    /// Overridden under <c>RateLimiting:Policies:identity.sign-in</c>.
    /// </summary>
    public static RateLimitPolicy SignInPolicy { get; } = new(30, TimeSpan.FromMinutes(15), RateLimitPartition.Ip);

    /// <summary>
    /// Password attempts for one email address (E03-02), from any network: at most 10 in 15 minutes, so a password is
    /// not guessed from many addresses at once. The partition is the email; <c>Partition</c> has no effect.
    /// </summary>
    public static RateLimitPolicy SignInAddressPolicy { get; } = new(10, TimeSpan.FromMinutes(15), RateLimitPartition.Ip);

    /// <summary>Sign-in codes requested from one network address (E03-02): at most 10 an hour.</summary>
    public static RateLimitPolicy SignInCodePolicy { get; } = new(10, TimeSpan.FromHours(1), RateLimitPartition.Ip);

    /// <summary>
    /// Letters with a sign-in code to one email address (E03-02): at most 5 an hour on top of the resend timer, so
    /// nobody's mailbox is flooded. The partition is the email; <c>Partition</c> has no effect.
    /// </summary>
    public static RateLimitPolicy SignInCodeAddressPolicy { get; } = new(5, TimeSpan.FromHours(1), RateLimitPartition.Ip);

    /// <summary>
    /// Failed password sign-ins and code entries from one network address (E03-12) after which every further attempt
    /// from it needs a captcha: 10 in 15 minutes. Overridden under <c>RateLimiting:Policies:identity.sign-in.failures</c>.
    /// </summary>
    public static RateLimitPolicy SignInFailurePolicy { get; } = new(10, TimeSpan.FromMinutes(15), RateLimitPartition.Ip);

    /// <summary>
    /// Failed password sign-ins to one email address (E03-12), from any network, after which every further attempt
    /// needs a captcha: 5 in 15 minutes. The partition is the email; <c>Partition</c> has no effect.
    /// </summary>
    public static RateLimitPolicy SignInAddressFailurePolicy { get; } = new(5, TimeSpan.FromMinutes(15), RateLimitPartition.Ip);

    /// <summary>
    /// Starts and callbacks of the sign-in with an external provider (ADR-0115) per network address: a person needs a
    /// few, a script trying codes or flooding the providers many.
    /// </summary>
    public static RateLimitPolicy ExternalSignInPolicy { get; } = new(30, TimeSpan.FromMinutes(15), RateLimitPartition.Ip);

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
        new(CaptchaProviderKey, "captcha after a series of failed sign-ins (ADR-0038): ProofOfWork, the platform's own, " +
            "or Fake, which accepts a fixed solution and is refused outside Development and Test")
        {
            Default = nameof(Captcha.CaptchaProvider.ProofOfWork),
            AllowedValues = Enum.GetNames<Captcha.CaptchaProvider>(),
        },
        .. RateLimitingSettings.Policy(Email.AccountEmailEndpoints.CodePolicy, EmailCodePolicy, "letters with an email confirmation code"),
        .. RateLimitingSettings.Policy(Registration.RegistrationEndpoints.Policy, RegistrationPolicy, "registrations and their new codes"),
        .. RateLimitingSettings.Policy(SignInEndpoints.AttemptPolicy, SignInPolicy, "password attempts and sign-in code entries"),
        .. RateLimitingSettings.Policy(SignInEndpoints.AddressAttemptPolicy, SignInAddressPolicy, "password attempts for one email address"),
        .. RateLimitingSettings.Policy(SignInEndpoints.FailurePolicy, SignInFailurePolicy, "failed sign-ins from one network address before a captcha is required"),
        .. RateLimitingSettings.Policy(SignInEndpoints.AddressFailurePolicy, SignInAddressFailurePolicy, "failed sign-ins to one email address before a captcha is required"),
        .. RateLimitingSettings.Policy(SignInEndpoints.CodePolicy, SignInCodePolicy, "sign-in code requests"),
        .. RateLimitingSettings.Policy(SignInEndpoints.AddressCodePolicy, SignInCodeAddressPolicy, "sign-in code letters to one email address"),
        .. RateLimitingSettings.Policy(External.ExternalSignInEndpoints.Policy, ExternalSignInPolicy, "starts and callbacks of the sign-in with an external provider"),
        .. External.ExternalSignInSettings.All,
    ];
}
