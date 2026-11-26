using System.Globalization;
using MailKit.Security;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Platform.Mail;

/// <summary>The mail service of a stand: SMTP, or the in-memory fake for tests.</summary>
public enum MailProvider
{
    Smtp,
    InMemory,
}

/// <summary>How the SMTP connection is protected: <c>StartTls</c> (port 587), <c>SslOnConnect</c> (465), <c>None</c> (Mailpit).</summary>
public enum SmtpSecurity
{
    StartTls,
    SslOnConnect,
    None,
}

/// <summary>The SMTP connection and the sender of every letter (<c>Mail:*</c>, docs/CONFIG.md).</summary>
public sealed record SmtpMailOptions(
    string Host,
    int Port,
    SmtpSecurity Security,
    string? Username,
    string? Password,
    string FromAddress,
    string FromName)
{
    public SecureSocketOptions SocketOptions => Security switch
    {
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.None => SecureSocketOptions.None,
        _ => SecureSocketOptions.StartTls,
    };
}

public static class MailServiceCollectionExtensions
{
    public const string ProviderKey = "Mail:Provider";
    public const string FromAddressKey = "Mail:From:Address";
    public const string FromNameKey = "Mail:From:Name";
    public const string SmtpHostKey = "Mail:Smtp:Host";
    public const string SmtpPortKey = "Mail:Smtp:Port";
    public const string SmtpSecurityKey = "Mail:Smtp:Security";
    public const string SmtpUsernameKey = "Mail:Smtp:Username";
    public const string SmtpPasswordKey = "Mail:Smtp:Password";
    public const int DefaultSmtpPort = 587;

    /// <summary>
    /// Registers <see cref="IMailer"/>, <see cref="IMailer{TContext}"/> and the templates: for any host that sends mail. The host also needs the bus
    /// (<c>AddPlatformMessaging</c>), which carries the rendered letter to the workers.
    /// </summary>
    public static IServiceCollection AddPlatformMail(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IMailTemplateSource>(EmbeddedMailTemplateSource.Instance);
        services.TryAddSingleton(MailBrand.Default);
        services.TryAddSingleton<MailTemplateRenderer>();
        services.TryAddScoped<IMailer, Mailer>();
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IMailer<>), typeof(ModuleMailer<>)));
        return services;
    }

    /// <summary>
    /// Registers the delivery of queued mail (the workers): the <see cref="IEmailSender"/> of <c>Mail:Provider</c>.
    /// The consumer is added to the bus with <see cref="AddMailDelivery"/>.
    /// </summary>
    public static IServiceCollection AddPlatformMailDelivery(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPlatformMail();
        if (ReadProvider(configuration) == MailProvider.InMemory)
        {
            services.TryAddSingleton<InMemoryEmailSender>();
            services.TryAddSingleton<IEmailSender>(sp => sp.GetRequiredService<InMemoryEmailSender>());
            return services;
        }

        // Read when first needed: the host has already checked the settings at start (ADR-0007).
        services.TryAddSingleton(sp => ReadSmtp(configuration, sp.GetRequiredService<MailBrand>()));
        services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }

    /// <summary>Adds <see cref="SendEmailConsumer"/>, the delivery of queued mail, to the bus.</summary>
    public static IBusRegistrationConfigurator AddMailDelivery(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.AddConsumer<SendEmailConsumer>();
        return bus;
    }

    // For the settings check: never throws, a wrong provider is reported by its own setting.
    internal static bool IsSmtp(IConfiguration configuration) =>
        !string.Equals(configuration[ProviderKey], nameof(MailProvider.InMemory), StringComparison.OrdinalIgnoreCase);

    private static MailProvider ReadProvider(IConfiguration configuration)
    {
        var value = configuration[ProviderKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return MailProvider.Smtp;
        }

        return Enum.TryParse<MailProvider>(value, ignoreCase: true, out var provider) && Enum.IsDefined(provider)
            ? provider
            : throw new InvalidOperationException(
                $"{ProviderKey} = '{value}': expected one of {string.Join(", ", Enum.GetNames<MailProvider>())}.");
    }

    private static SmtpMailOptions ReadSmtp(IConfiguration configuration, MailBrand brand)
    {
        string Required(string key) => configuration[key] is { Length: > 0 } v
            ? v
            : throw new InvalidOperationException($"{key} is required for {ProviderKey} = Smtp.");

        var port = configuration[SmtpPortKey] is { Length: > 0 } p
            ? int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is > 0 and < 65536
                ? n
                : throw new InvalidOperationException($"{SmtpPortKey} = '{p}': expected a port number.")
            : DefaultSmtpPort;
        var security = configuration[SmtpSecurityKey] is { Length: > 0 } s
            ? Enum.TryParse<SmtpSecurity>(s, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new InvalidOperationException(
                    $"{SmtpSecurityKey} = '{s}': expected one of {string.Join(", ", Enum.GetNames<SmtpSecurity>())}.")
            : SmtpSecurity.StartTls;
        var from = Required(FromAddressKey);
        if (!from.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{FromAddressKey} = '{from}': expected an email address.");
        }

        return new SmtpMailOptions(
            Required(SmtpHostKey),
            port,
            security,
            configuration[SmtpUsernameKey] is { Length: > 0 } user ? user : null,
            configuration[SmtpPasswordKey],
            from,
            configuration[FromNameKey] is { Length: > 0 } name ? name : brand.ProductName);
    }
}
