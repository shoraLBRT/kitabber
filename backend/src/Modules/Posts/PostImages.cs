using System.Text.Json.Nodes;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Posts;

/// <summary>
/// The pictures of a post's image blocks (E05-06, ADR-0049). A block names an uploaded file by its id; the post may
/// only show images its author uploaded, and the reader gets their signed sources next to the document, because the
/// document itself stores no links.
/// </summary>
public static class PostImages
{
    public const string NotYoursMessage = "Такого изображения нет среди ваших загрузок.";

    /// <summary>
    /// The widths offered for a picture of a post (ADR-0040): the text column is at most 680 CSS pixels wide, so 1920
    /// covers it at density 3 and 320 a narrow phone at density 1.
    /// </summary>
    public static readonly IReadOnlyList<ImageVariant> Variants =
    [
        ImageVariant.ToWidth(320, ImageFormat.WebP),
        ImageVariant.ToWidth(640, ImageFormat.WebP),
        ImageVariant.ToWidth(960, ImageFormat.WebP),
        ImageVariant.ToWidth(1280, ImageFormat.WebP),
        ImageVariant.ToWidth(1920, ImageFormat.WebP),
    ];

    /// <summary>The files the image blocks of a cleaned document name, by block index.</summary>
    public static IEnumerable<(int Index, Guid MediaId)> MediaIds(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document["blocks"] is not JsonArray blocks)
        {
            yield break;
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is JsonObject block
                && block["type"]?.GetValue<string>() == "image"
                && block["data"]?["media"]?.GetValue<string>() is { } value
                && Guid.TryParse(value, out var id))
            {
                yield return (i, id);
            }
        }
    }

    /// <summary>
    /// The image blocks that name a file which is not an image uploaded by <paramref name="accountId"/>, by the path
    /// <c>body.blocks[i].data.media</c>; empty when every picture is the author's own.
    /// </summary>
    public static async Task<Dictionary<string, string[]>> ProblemsAsync(
        JsonObject document, Guid accountId, IMediaLibrary media, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(media);
        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var checkedIds = new Dictionary<Guid, bool>();
        foreach (var (index, id) in MediaIds(document))
        {
            if (!checkedIds.TryGetValue(id, out var allowed))
            {
                var file = await media.FindAsync(id, cancellationToken);
                allowed = file is { Kind: MediaFileKind.Image } && file.OwnerAccountId == accountId;
                checkedIds[id] = allowed;
            }

            if (!allowed)
            {
                problems[$"body.blocks[{index}].data.media"] = [NotYoursMessage];
            }
        }

        return problems;
    }

    /// <summary>The sources of every picture in <paramref name="bodies"/>, one query for all of them, keyed by file id.</summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ImageSource>>> SourcesAsync(
        IEnumerable<JsonObject> bodies, IMediaLibrary media, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(media);
        var ids = bodies.SelectMany(b => MediaIds(b).Select(m => m.MediaId)).ToHashSet();
        return ids.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<ImageSource>>()
            : await media.ImageSourcesAsync(ids, Variants, cancellationToken);
    }
}
