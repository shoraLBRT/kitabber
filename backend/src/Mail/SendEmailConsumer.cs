using MassTransit;
using Microsoft.Extensions.Logging;

namespace Platform.Mail;

/// <summary>
/// Delivers queued email in the workers (ADR-0017). A failed delivery throws: the bus retries it (ADR-0005), then
/// moves it to the <c>send-email_error</c> queue, from which it can be moved back once the mail service works.
/// </summary>
public sealed partial class SendEmailConsumer(IEmailSender sender, ILogger<SendEmailConsumer> logger) : IConsumer<SendEmail>
{
    public async Task Consume(ConsumeContext<SendEmail> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await sender.SendAsync(context.Message, context.CancellationToken);
        LogSent(logger, context.Message.Template, context.Message.Id);
    }

    // The address and the text are personal data: only the template and the id go to the log.
    [LoggerMessage(Level = LogLevel.Information, Message = "Email {Template} {EmailId} sent")]
    private static partial void LogSent(ILogger logger, string template, Guid emailId);
}
