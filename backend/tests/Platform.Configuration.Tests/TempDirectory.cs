namespace Platform.Configuration.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "platform-config-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
