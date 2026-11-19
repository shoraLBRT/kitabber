using Microsoft.Extensions.Configuration;

namespace Platform.Configuration;

/// <summary>
/// Settings from a directory with one file per setting (ADR-0007): the file name is the environment-variable form of
/// the key (<c>ConnectionStrings__Redis</c>), the content is the value. This is the layout of Docker and Kubernetes
/// secrets, and any secret store can render it, so the platform does not depend on a particular store.
/// </summary>
public sealed class SecretFilesConfigurationSource(string directory) : IConfigurationSource
{
    public string Directory { get; } = directory;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new SecretFilesConfigurationProvider(Directory);
}

internal sealed class SecretFilesConfigurationProvider(string directory) : ConfigurationProvider
{
    public override void Load()
    {
        if (!System.IO.Directory.Exists(directory))
        {
            throw new InvalidConfigurationException(
                $"{PlatformConfigurationExtensions.SecretsDirectoryKey} = '{directory}': the directory does not exist");
        }

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in System.IO.Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(path);

            // Kubernetes keeps service entries such as ..data next to the secrets.
            if (name.StartsWith('.'))
            {
                continue;
            }

            data[name.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal)] =
                File.ReadAllText(path).TrimEnd('\r', '\n');
        }

        Data = data;
    }
}
