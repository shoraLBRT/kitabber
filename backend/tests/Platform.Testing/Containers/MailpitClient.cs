using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Platform.Testing.Containers;

/// <summary>
/// Reads the letters Mailpit caught (its API, https://mailpit.axllent.org/docs/api-v1/). Tests share one Mailpit,
/// so each test sends to an address of its own and looks for it with <see cref="WaitForAsync"/>.
/// </summary>
public sealed class MailpitClient(Uri baseAddress) : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = baseAddress };

    /// <summary>The letters to <paramref name="recipient"/>, newest first, without their bodies.</summary>
    public async Task<IReadOnlyList<MailpitSummary>> SearchAsync(string recipient, CancellationToken cancellationToken = default)
    {
        var query = Uri.EscapeDataString($"to:\"{recipient}\"");
        var result = await _http.GetFromJsonAsync<MailpitSearch>($"api/v1/search?query={query}", cancellationToken);
        return result?.Messages ?? [];
    }

    /// <summary>A whole letter by its Mailpit id.</summary>
    public async Task<MailpitMessage> GetAsync(string id, CancellationToken cancellationToken = default) =>
        await _http.GetFromJsonAsync<MailpitMessage>($"api/v1/message/{Uri.EscapeDataString(id)}", cancellationToken)
        ?? throw new InvalidOperationException($"Mailpit has no message {id}");

    /// <summary>Waits until a letter to <paramref name="recipient"/> arrives and returns it; fails after <paramref name="timeout"/>.</summary>
    public async Task<MailpitMessage> WaitForAsync(string recipient, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        while (true)
        {
            var found = await SearchAsync(recipient, cancellationToken);
            if (found.Count > 0)
            {
                return await GetAsync(found[0].Id, cancellationToken);
            }

            if (cts.IsCancellationRequested)
            {
                Assert.Fail($"no letter to {recipient} reached Mailpit in {timeout.TotalSeconds:0}s");
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed record MailpitSearch([property: JsonPropertyName("messages")] IReadOnlyList<MailpitSummary> Messages);
}

public sealed record MailpitAddress(
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("Address")] string Address);

public sealed record MailpitSummary(
    [property: JsonPropertyName("ID")] string Id,
    [property: JsonPropertyName("Subject")] string Subject);

public sealed record MailpitMessage(
    [property: JsonPropertyName("ID")] string Id,
    [property: JsonPropertyName("MessageID")] string MessageId,
    [property: JsonPropertyName("From")] MailpitAddress From,
    [property: JsonPropertyName("To")] IReadOnlyList<MailpitAddress> To,
    [property: JsonPropertyName("Subject")] string Subject,
    [property: JsonPropertyName("Text")] string Text,
    [property: JsonPropertyName("HTML")] string Html);
