using System.Globalization;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Platform.Messaging;

namespace Platform.Mail;

/// <summary>
/// Sends an email from a template (ADR-0017): renders it at once, so a wrong template or a missing value fails in the
/// caller, then queues it for the workers through the scope's plain <see cref="IPublishEndpoint"/>. That endpoint is
/// the bus in a host without modules and the outbox of the host's first module otherwise (ADR-0005, item 7), so a
/// module sends its mail through <see cref="IMailer{TContext}"/> instead.
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

/// <summary>
/// The mail of one module (ADR-0005, item 7): the letter goes into the outbox of <typeparamref name="TContext"/>
/// (<see cref="IModuleOutbox{TContext}"/>) and leaves for the workers only after that context's <c>SaveChanges</c>,
/// whatever place the module takes among the host's modules.
/// </summary>
#pragma warning disable CA1040 // The type parameter is the contract: it names the module whose outbox carries the letter.
public interface IMailer<TContext> : IMailer
    where TContext : DbContext;
#pragma warning restore CA1040

internal sealed class Mailer(MailTemplateRenderer renderer, IPublishEndpoint publish) : IMailer
{
    public Task SendAsync(
        string recipient,
        string templateName,
        CultureInfo culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        publish.Publish(Letter.Render(renderer, recipient, templateName, culture, values), cancellationToken);
}

internal sealed class ModuleMailer<TContext>(MailTemplateRenderer renderer, IModuleOutbox<TContext> outbox)
    : IMailer<TContext>
    where TContext : DbContext
{
    public Task SendAsync(
        string recipient,
        string templateName,
        CultureInfo culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default) =>
        outbox.Publish(Letter.Render(renderer, recipient, templateName, culture, values), cancellationToken);
}

internal static class Letter
{
    public static SendEmail Render(
        MailTemplateRenderer renderer,
        string recipient,
        string templateName,
        CultureInfo culture,
        IReadOnlyDictionary<string, string> values)
    {
        if (!MailboxAddress.TryParse(recipient, out var address) || string.IsNullOrEmpty(address.Address)
            || !address.Address.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("not an email address", nameof(recipient));
        }

        var email = renderer.Render(templateName, culture, values);
        return new SendEmail(NewId.NextGuid(), templateName, address.Address, email.Subject, email.Text, email.Html);
    }
}
