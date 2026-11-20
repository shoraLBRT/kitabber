using MailKit.Net.Smtp;
using MimeKit;

namespace Platform.Mail;

/// <summary>
/// Delivers email over SMTP with MailKit (ADR-0017): Mailpit locally and in tests, the stand's mail service otherwise
/// (<c>Mail:Smtp:*</c>, docs/CONFIG.md). One connection per email: enough while mail is rare.
/// </summary>
public sealed class SmtpEmailSender(SmtpMailOptions options, TimeProvider time) : IEmailSender
{
    public async Task SendAsync(SendEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Date = time.GetUtcNow();
        message.MessageId = $"{email.Id:N}@{options.FromAddress[(options.FromAddress.IndexOf('@', StringComparison.Ordinal) + 1)..]}";
        message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, options.SocketOptions, cancellationToken);
        if (!string.IsNullOrEmpty(options.Username))
        {
            await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
