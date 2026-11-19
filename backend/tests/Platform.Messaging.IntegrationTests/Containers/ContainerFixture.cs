using System.Text.RegularExpressions;
using DotNet.Testcontainers.Containers;

namespace Platform.Messaging.IntegrationTests.Containers;

/// <summary>
/// One container per test run, from the same image as the compose.yaml service. Without Docker the tests that need
/// it are skipped locally (or with VERIFY_SKIP_DOCKER=1); in CI they fail instead. The general test infrastructure
/// (E01-15, #8) is expected to take these fixtures over.
/// </summary>
public abstract class ContainerFixture<TContainer> : IAsyncLifetime
    where TContainer : class, IContainer
{
    private TContainer? _container;
    private string? _skipReason;

    private static bool InCi => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

    /// <summary>The image name in compose.yaml without the tag, for example <c>rabbitmq</c>.</summary>
    protected abstract string ImageName { get; }

    /// <summary>The running container; skips the current test when Docker is not available.</summary>
    protected TContainer Container
    {
        get
        {
            Assert.SkipWhen(_skipReason is not null, _skipReason ?? string.Empty);
            return _container!;
        }
    }

    public async ValueTask InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("VERIFY_SKIP_DOCKER") == "1" && !InCi)
        {
            _skipReason = "VERIFY_SKIP_DOCKER=1";
            return;
        }

        var image = ComposeImage(ImageName);
        try
        {
            _container = Build(image);
            await _container.StartAsync();
        }
        catch (Exception e) when (!InCi)
        {
            _skipReason = $"Docker is not available: {e.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    protected abstract TContainer Build(string image);

    private static string ComposeImage(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "compose.yaml")))
        {
            dir = dir.Parent;
        }

        var compose = dir is null ? string.Empty : File.ReadAllText(Path.Combine(dir.FullName, "compose.yaml"));
        var match = Regex.Match(compose, $@"image:\s*({Regex.Escape(name)}:\S+)");
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException($"compose.yaml with a {name} image was not found above the test binaries");
    }
}
