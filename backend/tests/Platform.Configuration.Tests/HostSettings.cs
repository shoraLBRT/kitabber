namespace Platform.Configuration.Tests;

/// <summary>
/// Where each host's settings files live. Api, Workers and Migrator all copy an <c>appsettings.json</c> into this
/// project's output folder and the last one built wins, so a test reads a host's settings from that host's own
/// source folder instead (#411).
/// </summary>
internal static class HostSettings
{
    public static string RepositoryRoot { get; } = FindRoot();

    /// <summary>The content root of a host, such as <c>Workers</c>: the folder of its <c>appsettings*.json</c>.</summary>
    public static string ContentRoot(string host) => Path.Combine(RepositoryRoot, "backend", "src", host);

    /// <summary>Arguments for <see cref="Workers.WorkerHost.CreateBuilder"/> that read the workers' own settings files.</summary>
    public static string[] Workers(params string[] args) => ["--contentRoot", ContentRoot("Workers"), .. args];

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "backend", "Platform.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("repository root (backend/Platform.slnx) not found");
    }
}
