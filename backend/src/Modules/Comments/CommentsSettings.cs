using System.Globalization;
using Microsoft.Extensions.Configuration;
using Platform.Configuration;

namespace Platform.Modules.Comments;

/// <summary>The limits of the comment tree, read once at start (ADR-0048, ADR-0059).</summary>
/// <param name="MaxDepth">The most levels of a branch: a reply to the post is level 1.</param>
/// <param name="EditMinutes">How long after writing it the author may change a comment's text, in minutes.</param>
public sealed record CommentLimits(int MaxDepth, int EditMinutes = CommentLimits.DefaultEditMinutes)
{
    public const int DefaultEditMinutes = 15;

    public static CommentLimits Default { get; } = new(8);

    /// <summary>The time to change a comment's text (<see cref="EditMinutes"/>).</summary>
    public TimeSpan EditWindow => TimeSpan.FromMinutes(EditMinutes);
}

/// <summary>The settings of the Comments module (ADR-0007, ADR-0048); each is described in docs/CONFIG.md.</summary>
public static class CommentsSettings
{
    public const string MaxDepthKey = "Comments:MaxDepth";

    public const string EditMinutesKey = "Comments:EditMinutes";

    /// <summary>The deepest branch the setting may ask for.</summary>
    public const int MaxDepthLimit = 32;

    /// <summary>The longest time to change a comment the setting may ask for: a day.</summary>
    public const int EditMinutesLimit = 1440;

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(MaxDepthKey, $"the most levels of a comment branch, 1 to {MaxDepthLimit}")
        {
            Default = CommentLimits.Default.MaxDepth.ToString(CultureInfo.InvariantCulture),
        },
        new(EditMinutesKey, $"how long the author may change a comment's text, in minutes, 1 to {EditMinutesLimit}")
        {
            Default = CommentLimits.Default.EditMinutes.ToString(CultureInfo.InvariantCulture),
        },
    ];

    /// <summary>
    /// The limits; a depth that is not a whole number from 1 to <see cref="MaxDepthLimit"/>, or a time to change a
    /// comment that is not one from 1 to <see cref="EditMinutesLimit"/>, stops the host.
    /// </summary>
    public static CommentLimits ReadLimits(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new CommentLimits(
            Read(configuration, MaxDepthKey, MaxDepthLimit, CommentLimits.Default.MaxDepth),
            Read(configuration, EditMinutesKey, EditMinutesLimit, CommentLimits.Default.EditMinutes));
    }

    private static int Read(IConfiguration configuration, string key, int limit, int fallback)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number >= 1 && number <= limit
                ? number
                : throw new InvalidOperationException($"{key}: '{value}' is not a whole number from 1 to {limit}.");
    }
}
