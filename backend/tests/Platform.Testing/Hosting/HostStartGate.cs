namespace Platform.Testing.Hosting;

/// <summary>
/// Lets only a few test hosts of one test process start at a time: build, start and migrate. xUnit runs the test
/// classes in parallel, and every class with a host starts it in its first test; started all at once on a CI runner
/// (four cores, several test assemblies at the same time), they starve each other, and a database connection opened
/// during the start can spend its whole Npgsql <c>Timeout</c> (15 seconds) before it even reaches the socket. A host
/// waiting here costs no connection time; once started, the hosts run their tests in parallel as before.
/// </summary>
public static class HostStartGate
{
    /// <summary>How many hosts may start at once: half the processors, at least one.</summary>
    public static int Limit { get; } = Math.Max(1, Environment.ProcessorCount / 2);

    // After Limit: static fields are initialized in the order they are written.
    private static readonly SemaphoreSlim _gate = new(Limit, Limit);

    /// <summary>Runs <paramref name="start"/> (the host's build, start and migrations) when a place is free.</summary>
    public static async Task RunAsync(Func<Task> start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await start();
        }
        finally
        {
            _gate.Release();
        }
    }
}
