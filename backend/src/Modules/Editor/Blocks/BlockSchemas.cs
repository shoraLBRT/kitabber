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

    /// <summary>
    /// Version 1, the blocks of milestone M1 without media: text, H2 and H3 headings, list, quote and delimiter
    /// (E05-02…E05-05). Media and the rest come with their tasks.
    /// </summary>
    public static BlockSchemaSet V1(InlineHtml html) => new(1,
    [
        new BlockSchema(Paragraph, new InlineHtmlField("text", html)),
        new BlockSchema(Header, new InlineHtmlField("text", html), new IntegerChoiceField("level", [2, 3])),
        new BlockSchema(List, new ChoiceField("style", ["unordered", "ordered"]), new InlineHtmlListField("items", html)),
        new BlockSchema(Quote, new InlineHtmlField("text", html), new InlineHtmlField("caption", html, defaultValue: string.Empty)),
        new BlockSchema(Delimiter),
    ]);

    /// <summary>All released versions, oldest first.</summary>
    public static IReadOnlyList<BlockSchemaSet> All(InlineHtml html) => [V1(html)];
}
