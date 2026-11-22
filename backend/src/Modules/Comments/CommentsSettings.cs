using System.Globalization;
using Microsoft.Extensions.Configuration;
using Platform.Configuration;

namespace Platform.Modules.Comments;

/// <summary>The limits of the comment tree, read once at start (ADR-0048).</summary>
/// <param name="MaxDepth">The most levels of a branch: a reply to the post is level 1.</param>
public sealed record CommentLimits(int MaxDepth)
{
    public static CommentLimits Default { get; } = new(8);
}

/// <summary>The settings of the Comments module (ADR-0007, ADR-0048); each is described in docs/CONFIG.md.</summary>
public static class CommentsSettings
{
    public const string MaxDepthKey = "Comments:MaxDepth";

    /// <summary>The deepest branch the setting may ask for.</summary>
    public const int MaxDepthLimit = 32;

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(MaxDepthKey, $"the most levels of a comment branch, 1 to {MaxDepthLimit}")
        {
            Default = CommentLimits.Default.MaxDepth.ToString(CultureInfo.InvariantCulture),
        },
    ];

    /// <summary>The limits; a depth that is not a whole number from 1 to <see cref="MaxDepthLimit"/> stops the host.</summary>
    public static CommentLimits ReadLimits(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[MaxDepthKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return CommentLimits.Default;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var depth)
            && depth is >= 1 and <= MaxDepthLimit
                ? new CommentLimits(depth)
                : throw new InvalidOperationException(
                    $"{MaxDepthKey}: '{value}' is not a whole number from 1 to {MaxDepthLimit}.");
    }
}
