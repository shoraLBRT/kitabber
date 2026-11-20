namespace Platform.Mail;

/// <summary>
/// Delivers one email to the mail service (ADR-0017): <see cref="SmtpEmailSender"/> in every stand,
/// <see cref="InMemoryEmailSender"/> in tests. A failure throws, and the queue retries the message.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(SendEmail email, CancellationToken cancellationToken);
}
