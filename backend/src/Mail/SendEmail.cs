namespace Platform.Mail;

/// <summary>
/// A rendered email waiting in the queue for delivery (ADR-0017). <see cref="IMailer"/> renders the template and
/// publishes it; the workers' <see cref="SendEmailConsumer"/> hands it to <see cref="IEmailSender"/>, retrying on a
/// failure. Only <see cref="Template"/> and <see cref="Id"/> may be logged: the rest is personal data.
/// </summary>
/// <param name="Id">Unique per email; the Message-ID of the letter, so a retried delivery is the same letter.</param>
/// <param name="Template">The template it was rendered from, for logs and metrics.</param>
/// <param name="To">The recipient's address.</param>
public sealed record SendEmail(Guid Id, string Template, string To, string Subject, string TextBody, string HtmlBody);
