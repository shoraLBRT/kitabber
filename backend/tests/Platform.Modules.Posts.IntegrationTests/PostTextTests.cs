namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>E14-01, ADR-0094: the text of a post for the search index — every text of its blocks, without markup.</summary>
public sealed class PostTextTests
{
    [Fact]
    public void TextOfEveryBlockComesWithoutMarkupABlockPerLine()
    {
        const string body = """
            {"version": 1, "blocks": [
              {"id": "a", "type": "header", "data": {"text": "Про <b>кот</b>ов", "level": 2}},
              {"id": "b", "type": "paragraph", "data": {"text": "Первая<br>строка &amp; вторая"}},
              {"id": "c", "type": "delimiter", "data": {}},
              {"id": "d", "type": "list", "data": {"style": "unordered", "items": ["раз", "<i>два</i>"], "levels": []}},
              {"id": "e", "type": "quote", "data": {"text": "Цитата", "caption": "Автор"}},
              {"id": "f", "type": "image", "data": {"media": "0193a2b0-0000-7000-8000-000000000000", "width": 10,
                "height": 10, "caption": ""}}
            ]}
            """;

        Assert.Equal("Про котов\nПервая строка & вторая\nраз\nдва\nЦитата\nАвтор", PostText.Of(body));
    }

    [Fact]
    public void DocumentWithoutBlocksHasNoText() => Assert.Equal(string.Empty, PostText.Of("""{"version": 1}"""));
}
