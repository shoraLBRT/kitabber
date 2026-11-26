using System.Globalization;
using Microsoft.Extensions.Configuration;
using Platform.Configuration;

namespace Platform.Modules.Posts;

/// <summary>The settings of the Posts module (ADR-0007); each is described in docs/CONFIG.md.</summary>
public static class PostsSettings
{
    /// <summary>Seconds between flushes of the view buffer on an instance (ADR-0083); 0 turns the flush off on it.</summary>
    public const string ViewFlushSecondsKey = "Posts:Views:FlushSeconds";

    public const int DefaultViewFlushSeconds = 5;

    /// <summary>The longest pause between flushes the setting may ask for: an hour.</summary>
    public const int ViewFlushSecondsLimit = 3600;

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(ViewFlushSecondsKey, $"seconds between flushes of the view counters to the database, 0 to {ViewFlushSecondsLimit}")
        {
            Default = DefaultViewFlushSeconds.ToString(CultureInfo.InvariantCulture),
        },
    ];

    /// <summary>
    /// The pause between flushes; <see cref="TimeSpan.Zero"/> when this instance does not flush. A value that is not a
    /// whole number from 0 to <see cref="ViewFlushSecondsLimit"/> stops the host.
    /// </summary>
    public static TimeSpan ReadViewFlushInterval(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[ViewFlushSecondsKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.FromSeconds(DefaultViewFlushSeconds);
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds <= ViewFlushSecondsLimit
                ? TimeSpan.FromSeconds(seconds)
                : throw new InvalidOperationException(
                    $"{ViewFlushSecondsKey}: '{value}' is not a whole number from 0 to {ViewFlushSecondsLimit}.");
    }
}
