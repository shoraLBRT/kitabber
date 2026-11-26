namespace Platform.Modules.Reactions;

/// <summary>
/// The reaction graphics the platform ships (E02-12, ADR-0095): Fluent Emoji Flat (MIT), one SVG per code in the web
/// client (<c>brand/reactions</c>), so a reaction looks the same on every system. The set of reactions may
/// offer only these codes; a new picture comes with a change of code, not through the directory.
/// </summary>
public static class ReactionGraphics
{
    public static IReadOnlyList<string> Codes { get; } = ["like", "heart", "smile", "clap", "idea", "wow", "sad"];

    public static bool Has(string? code) => code is not null && Codes.Contains(code, StringComparer.Ordinal);
}

/// <summary>
/// One entry of the directory of reactions (E02-12, FR-REACT, ADR-0095): which reaction the platform offers, under what
/// name and in what place. The administration changes the names, the order and which are offered (E15-12, #168); the
/// default set comes with the migration.
/// </summary>
public sealed class ReactionSetEntry
{
    public const int CodeMaxLength = 16;
    public const int TitleMaxLength = 40;

    /// <summary>The code of the reaction and of its picture, one of <see cref="ReactionGraphics.Codes"/>.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>The name a person sees and a screen reader reads.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The place in the set, from 1.</summary>
    public int Position { get; set; }

    /// <summary>The platform offers it; a reaction no longer offered stays on what it was put on.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The default set: restrained, without mockery or vulgarity (FR-REACT). «Нравится» is the like that already
    /// exists (E10-01), so its code stays <c>like</c>.
    /// </summary>
    public static IReadOnlyList<ReactionSetEntry> Defaults { get; } =
    [
        new() { Code = "like", Title = "Нравится", Position = 1 },
        new() { Code = "heart", Title = "Тепло", Position = 2 },
        new() { Code = "smile", Title = "Улыбка", Position = 3 },
        new() { Code = "clap", Title = "Браво", Position = 4 },
        new() { Code = "idea", Title = "Полезно", Position = 5 },
        new() { Code = "wow", Title = "Удивительно", Position = 6 },
        new() { Code = "sad", Title = "Грустно", Position = 7 },
    ];
}
