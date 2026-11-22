using System.Text;
using System.Text.Json.Nodes;
using Platform.Modules.Editor.Contracts;

namespace Platform.Modules.Posts;

/// <summary>
/// The checks before a post is published (FR-EDITOR, ADR-0046): it is not empty, its title is not too long, and the
/// post and each of its blocks fit their size. A draft is saved without them, so writing is never lost to a limit; a
/// published post is checked again on every edit.
/// </summary>
public static class PostChecks
{
    /// <summary>The longest title, in characters after trimming.</summary>
    public const int TitleMaxLength = 120;

    /// <summary>The largest block: its JSON in UTF-8, as stored.</summary>
    public const int BlockMaxBytes = 64 * 1024;

    /// <summary>The largest post body: the whole block document in UTF-8, as stored.</summary>
    public const int PostMaxBytes = 512 * 1024;

    public const string EmptyMessage = "Пустой пост опубликовать нельзя.";

    /// <summary>
    /// The problems that keep a post from being published, by field: <c>title</c>, <c>body</c> (empty or too large)
    /// and <c>body.blocks[i]</c> (a block too large); empty when it may be published.
    /// </summary>
    /// <param name="title">The cleaned title (<see cref="PostRules.CleanTitle"/>).</param>
    /// <param name="body">The cleaned block document as stored.</param>
    public static Dictionary<string, string[]> Problems(string? title, string body, IBlockDocumentValidator validator)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(validator);
        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (title is { Length: > TitleMaxLength })
        {
            problems["title"] = [$"Заголовок длиннее {TitleMaxLength} символов."];
        }

        var document = JsonNode.Parse(body)!.AsObject();
        if (!validator.HasContent(document))
        {
            problems["body"] = [EmptyMessage];
        }
        else if (Encoding.UTF8.GetByteCount(body) > PostMaxBytes)
        {
            problems["body"] = [$"Пост больше {PostMaxBytes / 1024} КБ."];
        }

        if (document["blocks"] is JsonArray blocks)
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] is { } block && Encoding.UTF8.GetByteCount(block.ToJsonString()) > BlockMaxBytes)
                {
                    problems[$"body.blocks[{i}]"] = [$"Блок больше {BlockMaxBytes / 1024} КБ."];
                }
            }
        }

        return problems;
    }
}
