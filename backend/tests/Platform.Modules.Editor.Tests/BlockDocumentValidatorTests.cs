using System.Text.Encodings.Web;
using System.Text.Json;
using Platform.Modules.Editor.Blocks;
using Platform.Modules.Editor.Contracts;

namespace Platform.Modules.Editor.Tests;

/// <summary>The block schemas of version 1 and the rules every version shares (ADR-0023).</summary>
public sealed class BlockDocumentValidatorTests
{
    private static readonly InlineHtml _html = new();
    private static readonly JsonSerializerOptions _readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly BlockDocumentValidator _validator = new(BlockSchemas.All(_html));

    [Fact]
    public void AcceptsEveryVersion1BlockAndReturnsTheCanonicalDocument()
    {
        var result = Validate("""
            {"blocks": [
              {"type": "paragraph", "id": "p1", "data": {"text": "Hello <b>world</b><script>alert(1)</script>"}},
              {"id": "h1", "type": "header", "data": {"level": 2, "text": "Heading"}},
              {"id": "l1", "type": "list", "data": {"style": "ordered", "items": ["one", "<i>two</i>"]}},
              {"id": "q1", "type": "quote", "data": {"text": "Quote"}},
              {"id": "d1", "type": "delimiter", "data": {}}
            ], "version": 1}
            """);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(
            """{"version":1,"blocks":[{"id":"p1","type":"paragraph","data":{"text":"Hello <b>world</b>"}},"""
            + """{"id":"h1","type":"header","data":{"text":"Heading","level":2}},"""
            + """{"id":"l1","type":"list","data":{"style":"ordered","items":["one","<i>two</i>"],"levels":[]}},"""
            + """{"id":"q1","type":"quote","data":{"text":"Quote","caption":""}},"""
            + """{"id":"d1","type":"delimiter","data":{}}]}""",
            result.Document!.ToJsonString(_readable));
    }

    [Fact]
    public void AcceptsAnEmptyDocument()
    {
        Assert.True(Validate("""{"version": 1, "blocks": []}""").IsValid);
    }

    [Theory]
    [InlineData("""{"id": "a", "type": "gallery", "data": {}}""", "blocks[0].type")]
    [InlineData("""{"id": "a", "type": "Paragraph", "data": {"text": ""}}""", "blocks[0].type")]
    [InlineData("""{"id": "a", "data": {"text": ""}}""", "blocks[0].type")]
    [InlineData("""{"id": "a", "type": "paragraph", "data": {"text": "", "html": "x"}}""", "blocks[0].data.html")]
    [InlineData("""{"id": "a", "type": "paragraph", "data": {"text": ""}, "tunes": {}}""", "blocks[0].tunes")]
    [InlineData("""{"id": "a", "type": "paragraph", "data": {"text": "", "text": "again"}}""", "blocks[0].data.text")]
    [InlineData("""{"id": "a", "type": "paragraph", "data": {}}""", "blocks[0].data.text")]
    [InlineData("""{"id": "a", "type": "paragraph", "data": {"text": 5}}""", "blocks[0].data.text")]
    [InlineData("""{"id": "a", "type": "paragraph"}""", "blocks[0].data")]
    [InlineData("""{"id": "a", "type": "paragraph", "data": "text"}""", "blocks[0].data")]
    [InlineData("""{"id": "a", "type": "header", "data": {"text": "", "level": 1}}""", "blocks[0].data.level")]
    [InlineData("""{"id": "a", "type": "header", "data": {"text": "", "level": "2"}}""", "blocks[0].data.level")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "checklist", "items": ["x"]}}""", "blocks[0].data.style")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "ordered", "items": []}}""", "blocks[0].data.items")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "ordered", "items": ["x", {"content": "y"}]}}""", "blocks[0].data.items[1]")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "ordered", "items": ["x", "y"], "levels": [0, 2]}}""", "blocks[0].data.levels[1]")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "ordered", "items": ["x", "y"], "levels": [0, "1"]}}""", "blocks[0].data.levels[1]")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "ordered", "items": ["x", "y"], "levels": [0, -1]}}""", "blocks[0].data.levels[1]")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "ordered", "items": ["x", "y"], "levels": 1}}""", "blocks[0].data.levels")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "ordered", "items": ["x", "y"], "levels": [0]}}""", "blocks[0].data.levels")]
    [InlineData("""{"id": "a", "type": "list", "data": {"style": "ordered", "items": ["x", "y"], "levels": [1, 0]}}""", "blocks[0].data.levels[0]")]
    [InlineData("""{"id": "a", "type": "delimiter", "data": {"line": true}}""", "blocks[0].data.line")]
    [InlineData("""{"id": "", "type": "delimiter", "data": {}}""", "blocks[0].id")]
    [InlineData("""{"id": "a b", "type": "delimiter", "data": {}}""", "blocks[0].id")]
    [InlineData("""{"type": "delimiter", "data": {}}""", "blocks[0].id")]
    [InlineData("""["delimiter"]""", "blocks[0]")]
    public void RejectsABadBlockAndNamesThePlace(string block, string path)
    {
        var result = Validate($$"""{"version": 1, "blocks": [{{block}}]}""");

        Assert.False(result.IsValid);
        Assert.Null(result.Document);
        Assert.Contains(result.Errors, e => e.Path == path);
    }

    [Fact]
    public void KeepsTheNestingLevelsOfAListItemByItem()
    {
        var result = Validate("""
            {"version": 1, "blocks": [
              {"id": "l", "type": "list", "data": {"levels": [0, 1, 1, 0], "style": "unordered", "items": ["a", "b", "c", "d"]}}
            ]}
            """);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(
            """{"style":"unordered","items":["a","b","c","d"],"levels":[0,1,1,0]}""",
            result.Document!["blocks"]![0]!["data"]!.ToJsonString(_readable));
    }

    [Fact]
    public void ReadsAListWithoutLevelsAsAFlatList()
    {
        var result = Validate("""
            {"version": 1, "blocks": [{"id": "l", "type": "list", "data": {"style": "ordered", "items": ["a", "b"]}}]}
            """);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Empty(result.Document!["blocks"]![0]!["data"]!["levels"]!.AsArray());
    }

    [Fact]
    public void NamesTheIndexOfTheBadBlock()
    {
        var result = Validate("""
            {"version": 1, "blocks": [
              {"id": "a", "type": "delimiter", "data": {}},
              {"id": "b", "type": "delimiter", "data": {}},
              {"id": "c", "type": "video", "data": {}}
            ]}
            """);

        Assert.Equal([new BlockDocumentError("blocks[2].type", "Unknown block type in schema version 1.")], result.Errors);
    }

    [Fact]
    public void RejectsTwoBlocksWithOneId()
    {
        var result = Validate("""
            {"version": 1, "blocks": [
              {"id": "a", "type": "delimiter", "data": {}},
              {"id": "a", "type": "delimiter", "data": {}}
            ]}
            """);

        Assert.Equal("blocks[1].id", Assert.Single(result.Errors).Path);
    }

    [Theory]
    [InlineData("""[]""", "")]
    [InlineData("""{"blocks": []}""", "version")]
    [InlineData("""{"version": 1}""", "blocks")]
    [InlineData("""{"version": 1, "blocks": {}}""", "blocks")]
    [InlineData("""{"version": 1, "blocks": [], "time": 1}""", "time")]
    public void RejectsABadDocumentAndNamesThePlace(string document, string path)
    {
        var result = Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == path);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("\"1\"")]
    [InlineData("1.5")]
    public void RejectsAnUnsupportedSchemaVersion(string version)
    {
        var result = Validate($$"""{"version": {{version}}, "blocks": []}""");

        Assert.Equal(new BlockDocumentError("version", "Unsupported schema version; supported: 1."), Assert.Single(result.Errors));
    }

    [Fact]
    public void ReportsAtMostTheErrorLimit()
    {
        var blocks = string.Join(',', Enumerable.Range(0, 200).Select(i => $$$"""{"id": "b{{{i}}}", "type": "nope", "data": {}}"""));

        Assert.Equal(BlockDocumentValidator.MaxErrors, Validate($$"""{"version": 1, "blocks": [{{blocks}}]}""").Errors.Count);
    }

    [Fact]
    public void ValidatesEachDocumentByTheSchemasOfItsOwnVersion()
    {
        // A hypothetical version 2 in which headings gain level 4 and the quote loses its caption: a stored version 1
        // document is still read by the version 1 rules.
        var v2 = new BlockSchemaSet(2,
        [
            new BlockSchema(BlockSchemas.Header, new InlineHtmlField("text", _html), new IntegerChoiceField("level", [2, 3, 4])),
            new BlockSchema(BlockSchemas.Quote, new InlineHtmlField("text", _html)),
        ]);
        var validator = new BlockDocumentValidator([BlockSchemas.V1(_html), v2]);

        Assert.Equal(2, validator.CurrentVersion);
        Assert.Equal([1, 2], validator.SupportedVersions);

        const string heading4 = """{"id": "a", "type": "header", "data": {"text": "x", "level": 4}}""";
        const string captioned = """{"id": "a", "type": "quote", "data": {"text": "x", "caption": "y"}}""";
        Assert.True(Validate(validator, $$"""{"version": 2, "blocks": [{{heading4}}]}""").IsValid);
        Assert.False(Validate(validator, $$"""{"version": 1, "blocks": [{{heading4}}]}""").IsValid);
        Assert.True(Validate(validator, $$"""{"version": 1, "blocks": [{{captioned}}]}""").IsValid);
        Assert.Equal("blocks[0].data.caption",
            Assert.Single(Validate(validator, $$"""{"version": 2, "blocks": [{{captioned}}]}""").Errors).Path);
        Assert.Equal(1, Validate(validator, """{"version": 1, "blocks": []}""").Document!["version"]!.GetValue<int>());
    }

    [Fact]
    public void TheCurrentVersionIsTheLatestReleased()
    {
        Assert.Equal(1, _validator.CurrentVersion);
        Assert.Equal([1], _validator.SupportedVersions);
    }

    [Theory]
    [InlineData("""{"id": "p1", "type": "paragraph", "data": {"text": "Слово"}}""")]
    [InlineData("""{"id": "p1", "type": "paragraph", "data": {"text": "<b>&nbsp;x</b>"}}""")]
    [InlineData("""{"id": "h1", "type": "header", "data": {"text": "Заголовок", "level": 2}}""")]
    [InlineData("""{"id": "l1", "type": "list", "data": {"style": "unordered", "items": ["", "пункт"]}}""")]
    [InlineData("""{"id": "q1", "type": "quote", "data": {"text": " ", "caption": "Автор"}}""")]
    public void BlockWithTextIsContent(string block)
    {
        var result = Validate($$$"""{"version": 1, "blocks": [{"id": "d1", "type": "delimiter", "data": {}}, {{{block}}}]}""");

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.True(_validator.HasContent(result.Document!));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"id": "d1", "type": "delimiter", "data": {}}]""")]
    [InlineData("""[{"id": "p1", "type": "paragraph", "data": {"text": "  <b> </b><br>&nbsp;​"}}]""")]
    [InlineData("""[{"id": "h1", "type": "header", "data": {"text": "", "level": 3}}]""")]
    [InlineData("""[{"id": "l1", "type": "list", "data": {"style": "ordered", "items": ["", " "]}}]""")]
    [InlineData("""[{"id": "q1", "type": "quote", "data": {"text": "", "caption": ""}}]""")]
    public void DocumentWithoutTextIsEmpty(string blocks)
    {
        var result = Validate($$"""{"version": 1, "blocks": {{blocks}}}""");

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.False(_validator.HasContent(result.Document!));
    }

    [Fact]
    public void ImageKeepsTheFileItsSizeAndACleanCaption()
    {
        var result = Validate("""
            {"version": 1, "blocks": [{"id": "i1", "type": "image", "data": {
              "caption": "Закат <b>над</b> морем<script>x</script>", "height": 600, "width": 800,
              "media": "0192F0A1-2B3C-7D4E-8F90-A1B2C3D4E5F6"}}]}
            """);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(
            """{"version":1,"blocks":[{"id":"i1","type":"image","data":{"media":"0192f0a1-2b3c-7d4e-8f90-a1b2c3d4e5f6","""
            + "\"width\":800,\"height\":600,\"caption\":\"Закат <b>над</b> морем\"}}]}",
            result.Document!.ToJsonString(_readable));
        Assert.True(_validator.HasContent(result.Document!));
    }

    [Theory]
    [InlineData("""{"width": 1, "height": 1}""", "blocks[0].data.media")]
    [InlineData("""{"media": "not-a-uuid", "width": 1, "height": 1}""", "blocks[0].data.media")]
    [InlineData("""{"media": "00000000-0000-0000-0000-000000000000", "width": 1, "height": 1}""", "blocks[0].data.media")]
    [InlineData("""{"media": "0192f0a1-2b3c-7d4e-8f90-a1b2c3d4e5f6", "width": 0, "height": 1}""", "blocks[0].data.width")]
    [InlineData("""{"media": "0192f0a1-2b3c-7d4e-8f90-a1b2c3d4e5f6", "width": 1, "height": 20001}""", "blocks[0].data.height")]
    [InlineData("""{"media": "0192f0a1-2b3c-7d4e-8f90-a1b2c3d4e5f6", "width": 1, "height": 1, "url": "x"}""", "blocks[0].data.url")]
    public void ImageWithoutAValidFileOrSizeIsRejected(string data, string path)
    {
        var result = Validate($$$"""{"version": 1, "blocks": [{"id": "i1", "type": "image", "data": {{{data}}}}]}""");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == path);
    }

    [Fact]
    public void ImageCaptionIsLimitedByTheTextAReaderSees()
    {
        // Markup does not count, an entity and a line break count as one character each.
        var atLimit = "<b>" + new string('а', BlockSchemas.ImageCaptionMaxLength - 2) + "</b>&amp;<br>";
        var overLimit = new string('а', BlockSchemas.ImageCaptionMaxLength + 1);

        Assert.True(Validate(ImageWithCaption(atLimit)).IsValid);
        var result = Validate(ImageWithCaption(overLimit));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "blocks[0].data.caption");
    }

    private static string ImageWithCaption(string caption) => $$$"""
        {"version": 1, "blocks": [{"id": "i1", "type": "image", "data": {
          "media": "0192f0a1-2b3c-7d4e-8f90-a1b2c3d4e5f6", "width": 4, "height": 3, "caption": "{{{caption}}}"}}]}
        """;

    [Fact]
    public void DocumentOfAnUnknownVersionHasNoContent() =>
        Assert.False(_validator.HasContent(new System.Text.Json.Nodes.JsonObject { ["version"] = 99, ["blocks"] = new System.Text.Json.Nodes.JsonArray() }));

    private BlockDocumentResult Validate(string json) => Validate(_validator, json);

    private static BlockDocumentResult Validate(BlockDocumentValidator validator, string json)
    {
        using var document = JsonDocument.Parse(json);
        return validator.Validate(document.RootElement);
    }
}
