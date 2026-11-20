using System.Globalization;
using MassTransit;
using MimeKit;

namespace Platform.Mail;

/// <summary>
/// Sends an email from a template (ADR-0017): renders it at once, so a wrong template or a missing value fails in the
/// caller, then queues it for the workers. Inside a module's transaction the message goes through its outbox.
/// </summary>
public interface IMailer
{
    /// <param name="recipient">The recipient's address.</param>
    /// <param name="templateName">Template name, for example <c>login-code</c> (backend/src/Mail/Templates).</param>
    /// <param name="culture">The recipient's language; a template missing in it falls back to its parent, then to Russian.</param>
    /// <param name="values">The template's placeholders; text, escaped in HTML.</param>
    Task SendAsync(
        string recipient,
        string templateName,
        CultureInfo culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);
}

internal sealed class Mailer(MailTemplateRenderer renderer, IPublishEndpoint publish) : IMailer
{
    public Task SendAsync(
        string recipient,
        string templateName,
        CultureInfo culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        if (!MailboxAddress.TryParse(recipient, out var address) || string.IsNullOrEmpty(address.Address)
            || !address.Address.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("not an email address", nameof(recipient));
        }

        var email = renderer.Render(templateName, culture, values);
        return publish.Publish(
            new SendEmail(NewId.NextGuid(), templateName, address.Address, email.Subject, email.Text, email.Html),
            cancellationToken);
    }
}
