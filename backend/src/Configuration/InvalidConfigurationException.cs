namespace Platform.Configuration;

/// <summary>The configuration is incomplete or wrong; the message names every problem and how to fix it.</summary>
public sealed class InvalidConfigurationException : Exception
{
    public InvalidConfigurationException()
        : this([])
    {
    }

    public InvalidConfigurationException(string message)
        : this([message])
    {
    }

    public InvalidConfigurationException(string message, Exception innerException)
        : base(message, innerException) => Problems = [message];

    public InvalidConfigurationException(IReadOnlyList<string> problems)
        : base(Format(problems)) => Problems = problems;

    /// <summary>One line per problem, without secret values.</summary>
    public IReadOnlyList<string> Problems { get; }

    private static string Format(IReadOnlyList<string> problems) =>
        "Configuration is invalid; the host does not start (see docs/CONFIG.md):"
        + string.Concat(problems.Select(p => Environment.NewLine + " - " + p));
}
