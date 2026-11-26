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

    /// <summary>Seconds between hotness passes on an instance (ADR-0086); 0 turns the passes off on it.</summary>
    public const string HotnessIntervalSecondsKey = "Posts:Hotness:IntervalSeconds";

    public const int DefaultHotnessIntervalSeconds = 60;

    /// <summary>Days after publication during which a pass reads a post's reactions, comments and views again.</summary>
    public const string HotnessWindowDaysKey = "Posts:Hotness:WindowDays";

    /// <summary>The weight of a point of the post's rating in its hotness points.</summary>
    public const string HotnessRatingWeightKey = "Posts:Hotness:RatingWeight";

    /// <summary>The weight of a shown comment in the post's hotness points.</summary>
    public const string HotnessCommentWeightKey = "Posts:Hotness:CommentWeight";

    /// <summary>The weight of an opening of the post in its hotness points.</summary>
    public const string HotnessViewWeightKey = "Posts:Hotness:ViewWeight";

    /// <summary>Hours of age that weigh as much as ten times the points.</summary>
    public const string HotnessDecayHoursKey = "Posts:Hotness:DecayHours";

    /// <summary>The largest weight a coefficient of hotness may have.</summary>
    public const double HotnessWeightLimit = 1000;

    /// <summary>The longest decay and window: a year.</summary>
    public const int HotnessYearLimit = 365;

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(ViewFlushSecondsKey, $"seconds between flushes of the view counters to the database, 0 to {ViewFlushSecondsLimit}")
        {
            Default = DefaultViewFlushSeconds.ToString(CultureInfo.InvariantCulture),
        },
        new(HotnessIntervalSecondsKey, $"seconds between hotness passes, 0 to {ViewFlushSecondsLimit}")
        {
            Default = DefaultHotnessIntervalSeconds.ToString(CultureInfo.InvariantCulture),
        },
        new(HotnessWindowDaysKey, $"days after publication a post's hotness is read again, 1 to {HotnessYearLimit}")
        {
            Default = Format(HotnessCoefficients.Default.WindowDays),
        },
        new(HotnessRatingWeightKey, "hotness points per point of rating")
        {
            Default = Format(HotnessCoefficients.Default.RatingWeight),
        },
        new(HotnessCommentWeightKey, "hotness points per shown comment")
        {
            Default = Format(HotnessCoefficients.Default.CommentWeight),
        },
        new(HotnessViewWeightKey, "hotness points per opening")
        {
            Default = Format(HotnessCoefficients.Default.ViewWeight),
        },
        new(HotnessDecayHoursKey, "hours of age that weigh as much as ten times the points")
        {
            Default = Format(HotnessCoefficients.Default.DecayHours),
        },
    ];

    /// <summary>
    /// The pause between hotness passes; <see cref="TimeSpan.Zero"/> when this instance does not compute hotness. A
    /// value that is not a whole number from 0 to <see cref="ViewFlushSecondsLimit"/> stops the host.
    /// </summary>
    public static TimeSpan ReadHotnessInterval(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[HotnessIntervalSecondsKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.FromSeconds(DefaultHotnessIntervalSeconds);
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds <= ViewFlushSecondsLimit
                ? TimeSpan.FromSeconds(seconds)
                : throw new InvalidOperationException(
                    $"{HotnessIntervalSecondsKey}: '{value}' is not a whole number from 0 to {ViewFlushSecondsLimit}.");
    }

    /// <summary>
    /// The coefficients of the hotness formula as the configuration has them now (ADR-0086). They are read again
    /// before every pass, so a change of a source that reloads (a settings file) applies without a restart. A weight
    /// that is not a number from 0 to <see cref="HotnessWeightLimit"/>, a decay that is not above 0 and up to a year in
    /// hours, or a window that is not a whole number of days from 1 to <see cref="HotnessYearLimit"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public static HotnessCoefficients ReadHotness(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var defaults = HotnessCoefficients.Default;
        var window = configuration[HotnessWindowDaysKey];
        var days = defaults.WindowDays;
        if (!string.IsNullOrWhiteSpace(window)
            && (!int.TryParse(window, NumberStyles.None, CultureInfo.InvariantCulture, out days)
                || days < 1 || days > HotnessYearLimit))
        {
            throw new InvalidOperationException(
                $"{HotnessWindowDaysKey}: '{window}' is not a whole number from 1 to {HotnessYearLimit}.");
        }

        return new HotnessCoefficients(
            Weight(configuration, HotnessRatingWeightKey, defaults.RatingWeight),
            Weight(configuration, HotnessCommentWeightKey, defaults.CommentWeight),
            Weight(configuration, HotnessViewWeightKey, defaults.ViewWeight),
            Decay(configuration, defaults.DecayHours),
            days);
    }

    private static double Weight(IConfiguration configuration, string key, double fallback)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var weight)
            && weight <= HotnessWeightLimit
                ? weight
                : throw new InvalidOperationException($"{key}: '{value}' is not a number from 0 to {HotnessWeightLimit}.");
    }

    private static double Decay(IConfiguration configuration, double fallback)
    {
        var value = configuration[HotnessDecayHoursKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        const int limit = HotnessYearLimit * 24;
        return double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hours)
            && hours > 0 && hours <= limit
                ? hours
                : throw new InvalidOperationException(
                    $"{HotnessDecayHoursKey}: '{value}' is not a number of hours above 0 and up to {limit}.");
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

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
