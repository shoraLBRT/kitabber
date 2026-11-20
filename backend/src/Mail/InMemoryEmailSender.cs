using System.Collections.Concurrent;

namespace Platform.Mail;

/// <summary>The fake mail service (<c>Mail:Provider=InMemory</c>): keeps every email it is given, sends nothing.</summary>
public sealed class InMemoryEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SendEmail> _sent = new();

    /// <summary>The emails delivered so far, oldest first.</summary>
    public IReadOnlyCollection<SendEmail> Sent => [.. _sent];

    public Task SendAsync(SendEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        _sent.Enqueue(email);
        return Task.CompletedTask;
    }
}
