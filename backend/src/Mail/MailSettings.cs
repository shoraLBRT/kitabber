using Platform.Configuration;
using static Platform.Mail.MailServiceCollectionExtensions;

namespace Platform.Mail;

/// <summary>The settings <see cref="MailServiceCollectionExtensions.AddPlatformMailDelivery"/> reads (docs/CONFIG.md).</summary>
public static class MailSettings
{
    public static IReadOnlyList<Setting> All { get; } =
    [
        new(ProviderKey, "mail service: Smtp, or InMemory for tests")
        {
            Default = nameof(MailProvider.Smtp),
            AllowedValues = Enum.GetNames<MailProvider>(),
        },
        new(SmtpHostKey, "SMTP server of the mail service, for example smtp.example.org") { RequiredWhen = IsSmtp },
        new(SmtpPortKey, "SMTP port: 587 with StartTls, 465 with SslOnConnect") { Default = "587" },
        new(SmtpSecurityKey, "SMTP connection protection: StartTls, SslOnConnect, or None (Mailpit only)")
        {
            Default = nameof(SmtpSecurity.StartTls),
            AllowedValues = Enum.GetNames<SmtpSecurity>(),
        },
        new(SmtpUsernameKey, "SMTP login; without it the server is used without authentication"),
        new(SmtpPasswordKey, "SMTP password or the mail service's key") { Secret = true },
        new(FromAddressKey, "sender address of every letter, for example noreply@example.org") { RequiredWhen = IsSmtp },
        new(FromNameKey, "sender name; the product name from brand/brand.json when not set"),
    ];
}
