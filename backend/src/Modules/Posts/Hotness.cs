using System.Globalization;

namespace Platform.Modules.Posts;

/// <summary>
/// The coefficients of the hotness formula (E08-05, ADR-0086), from the settings <c>Posts:Hotness:*</c>. The defaults
/// are a proposal awaiting the owner's confirmation.
/// </summary>
/// <param name="RatingWeight">Points per point of the post's rating (ADR-0081).</param>
/// <param name="CommentWeight">Points per shown comment.</param>
/// <param name="ViewWeight">Points per opening of the post (ADR-0071).</param>
/// <param name="DecayHours">Hours of age that weigh as much as ten times the points.</param>
/// <param name="WindowDays">
/// Days after publication during which a pass reads the post's inputs again; later its score stays as it was.
/// </param>
public sealed record HotnessCoefficients(
    double RatingWeight, double CommentWeight, double ViewWeight, double DecayHours, int WindowDays)
{
    /// <summary>The proposed defaults (ADR-0086): a like 1, a comment 2, an opening 0.02, ten times per 12.5 hours.</summary>
    public static HotnessCoefficients Default { get; } = new(1, 2, 0.02, 12.5, 7);

    /// <summary>
    /// The coefficients a score depends on, as text: a stored score with another formula is computed again. The window
    /// is not part of it, because it does not change a score.
    /// </summary>
    public string Formula => string.Create(
        CultureInfo.InvariantCulture, $"r={RatingWeight:R};c={CommentWeight:R};v={ViewWeight:R};d={DecayHours:R}");
}

/// <summary>What a post's hotness is computed from.</summary>
/// <param name="Rating">The post's rating from its reactions (ADR-0081).</param>
/// <param name="Comments">The number of its shown comments.</param>
/// <param name="Views">The number of its openings (ADR-0071).</param>
public readonly record struct HotnessInputs(int Rating, int Comments, int Views);

/// <summary>
/// The hotness formula (E08-05, ADR-0086), the logarithmic one of Reddit's "hot": the order of magnitude of the points
/// plus the age as a publication time. The age part of a post never changes, so a score changes only when the post's
/// inputs or the coefficients change, and a pass computes again only those posts: a newer post ranks above an older one
/// unless the older has ten times the points for every <see cref="HotnessCoefficients.DecayHours"/> between them.
/// </summary>
public static class HotnessFormula
{
    /// <summary>The moment the age part counts from; it shifts every score alike, so it never changes the order.</summary>
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The points of a post: its inputs by their weights.</summary>
    public static double Points(HotnessInputs inputs, HotnessCoefficients coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        return (coefficients.RatingWeight * inputs.Rating)
            + (coefficients.CommentWeight * inputs.Comments)
            + (coefficients.ViewWeight * inputs.Views);
    }

    /// <summary>
    /// The score: <c>sign(points) · log10(max(|points|, 1)) + (published − epoch) / decay</c>, rounded to seven
    /// decimals so that equal posts get equal scores and the order breaks the tie by the id.
    /// </summary>
    public static double Score(HotnessInputs inputs, DateTimeOffset publishedAt, HotnessCoefficients coefficients)
    {
        var points = Points(inputs, coefficients);
        var order = Math.Log10(Math.Max(Math.Abs(points), 1));
        var age = (publishedAt - Epoch).TotalHours / coefficients.DecayHours;
        return Math.Round((Math.Sign(points) * order) + age, 7, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
/// The coefficients the stored scores were computed with (ADR-0086): a pass compares them with the settings and computes
/// every score again only when they differ, so an unchanged formula costs one read of this row.
/// </summary>
public sealed class HotnessFormulaState
{
    /// <summary>The id of the only row.</summary>
    public const int Current = 1;

    public int Id { get; init; } = Current;

    public string Formula { get; set; } = string.Empty;
}

/// <summary>
/// The hotness of a published post (ADR-0086): the inputs it was computed from, so a pass sees what changed, and the
/// score the popular feed is ordered by. A row whose post is no longer published is removed by the next pass.
/// </summary>
public sealed class PostHotness
{
    public Guid PostId { get; init; }

    /// <summary>When the post was published: the age part of the score.</summary>
    public DateTimeOffset PublishedAt { get; private set; }

    public int Rating { get; private set; }

    public int Comments { get; private set; }

    public int Views { get; private set; }

    public double Score { get; private set; }

    /// <summary>The coefficients the score was computed with (<see cref="HotnessCoefficients.Formula"/>).</summary>
    public string Formula { get; private set; } = string.Empty;

    public DateTimeOffset ComputedAt { get; private set; }

    public HotnessInputs Inputs => new(Rating, Comments, Views);

    public static PostHotness Compute(
        Guid postId, DateTimeOffset publishedAt, HotnessInputs inputs, HotnessCoefficients coefficients, DateTimeOffset now)
    {
        var hotness = new PostHotness { PostId = postId };
        hotness.Update(publishedAt, inputs, coefficients, now);
        return hotness;
    }

    /// <summary>Whether the stored score is the one these inputs and coefficients give.</summary>
    public bool IsCurrent(DateTimeOffset publishedAt, HotnessInputs inputs, HotnessCoefficients coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        return PublishedAt == publishedAt && Inputs == inputs && Formula == coefficients.Formula;
    }

    public void Update(DateTimeOffset publishedAt, HotnessInputs inputs, HotnessCoefficients coefficients, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        PublishedAt = publishedAt;
        (Rating, Comments, Views) = inputs;
        Score = HotnessFormula.Score(inputs, publishedAt, coefficients);
        Formula = coefficients.Formula;
        ComputedAt = now;
    }
}
