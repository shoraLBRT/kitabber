using System.Text.Json.Nodes;
using Platform.Modules.Editor.Contracts;

namespace Platform.Modules.Editor.Blocks;

/// <summary>
/// Every released version of the block schemas (ADR-0023). A new block type is added to the current version; a change
/// to an existing block that a stored document would fail or read differently is a new version, added here next to
/// the old ones.
/// </summary>
public static class BlockSchemas
{
    public const string Paragraph = "paragraph";
    public const string Header = "header";
    public const string List = "list";
    public const string Quote = "quote";
    public const string Delimiter = "delimiter";
    public const string Image = "image";

    /// <summary>The longest caption of an image, in characters a reader sees (E05-06, ADR-0049).</summary>
    public const int ImageCaptionMaxLength = 300;

    /// <summary>The longest side of an image the block may describe, in pixels.</summary>
    public const int ImageMaxSide = 20_000;

    /// <summary>The deepest nesting of a list item: one level under the top (E05-04, ADR-0033).</summary>
    public const int MaxListLevel = 1;

    /// <summary>
    /// Version 1, the blocks of milestone M1: text, H2 and H3 headings, list, quote and delimiter (E05-02…E05-05) and
    /// one image with a caption (E05-06, ADR-0049). Video, galleries and the rest come with their tasks.
    /// </summary>
    public static BlockSchemaSet V1(InlineHtml html) => new(1,
    [
        new BlockSchema(Paragraph, new InlineHtmlField("text", html)),
        new BlockSchema(Header, new InlineHtmlField("text", html), new IntegerChoiceField("level", [2, 3])),
        new BlockSchema(
            List,
            new ChoiceField("style", ["unordered", "ordered"]),
            new InlineHtmlListField("items", html),
            new LevelListField("levels", MaxListLevel)) { Check = CheckListLevels },
        new BlockSchema(Quote, new InlineHtmlField("text", html), new InlineHtmlField("caption", html, defaultValue: string.Empty)),
        new BlockSchema(Delimiter),
        new BlockSchema(
            Image,
            new MediaIdField("media"),
            new PixelSizeField("width", ImageMaxSide),
            new PixelSizeField("height", ImageMaxSide),
            new InlineHtmlField("caption", html, defaultValue: string.Empty, maxTextLength: ImageCaptionMaxLength)),
    ]);

    // A list's levels are empty (every item at the top, as in documents saved before E05-04) or one per item; the first
    // item has no parent, so it is at the top (ADR-0033).
    private static void CheckListLevels(JsonObject data, string path, ICollection<BlockDocumentError> errors)
    {
        var levels = data["levels"]!.AsArray();
        if (levels.Count == 0)
        {
            return;
        }

        var levelsPath = JsonObjects.Child(path, "levels");
        if (levels.Count != data["items"]!.AsArray().Count)
        {
            errors.Add(new(levelsPath, "Must be empty or give one level per item."));
        }
        else if (levels[0]!.GetValue<int>() != 0)
        {
            errors.Add(new($"{levelsPath}[0]", "The first item cannot be nested."));
        }
    }

    /// <summary>All released versions, oldest first.</summary>
    public static IReadOnlyList<BlockSchemaSet> All(InlineHtml html) => [V1(html)];
}
