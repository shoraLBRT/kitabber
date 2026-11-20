using System.Runtime.CompilerServices;

namespace Platform.Mail.Tests;

/// <summary>
/// Snapshot checks of rendered letters: the text is compared with <c>Snapshots/&lt;name&gt;.snap</c> next to this file.
/// A change of a template changes its snapshot; review the diff and rewrite the snapshots with
/// <c>UPDATE_SNAPSHOTS=1 dotnet test</c>. A missing snapshot is written and the test fails, so it gets reviewed;
/// in CI nothing is written.
/// </summary>
internal static class Snapshot
{
    public static void Match(string name, string actual, [CallerFilePath] string caller = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(caller)!, "Snapshots", $"{name}.snap");
        actual = actual.Replace("\r\n", "\n", StringComparison.Ordinal);
        var update = Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1";
        var inCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
        if (!File.Exists(path) || update)
        {
            if (inCi)
            {
                Assert.Fail($"snapshot {path} is missing: run UPDATE_SNAPSHOTS=1 dotnet test locally and commit it");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var existed = File.Exists(path);
            File.WriteAllText(path, actual);
            Assert.True(existed, $"snapshot {path} was written: review it and commit it");
            return;
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(expected, actual);
    }
}
