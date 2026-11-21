using System.Text;

namespace Platform.Modules.Media.Uploads;

/// <summary>What a media file is (FR-MEDIA): each kind has its own size limit.</summary>
public enum MediaKind
{
    Image = 1,
    Video = 2,
    Audio = 3,

    /// <summary>An attached file that is neither an image, a video nor audio.</summary>
    File = 4,
}

/// <summary>A file format the platform accepts.</summary>
/// <param name="ContentType">The MIME type the original is stored and served with.</param>
/// <param name="Kind">The kind, which picks the size limit.</param>
/// <param name="Extensions">The file name extensions of the format, lower case without the dot; the first is the usual one.</param>
public sealed record FileFormat(string ContentType, MediaKind Kind, IReadOnlyList<string> Extensions);

/// <summary>
/// The accepted formats and their recognition by content (ADR-0030): the leading bytes of the file, its signature,
/// decide what it is; the name and the declared Content-Type do not. A file whose bytes match no format here is not
/// accepted at all.
/// </summary>
public static class FileFormats
{
    /// <summary>How many leading bytes <see cref="Detect"/> needs to recognise every format.</summary>
    public const int HeaderLength = 64;

    public static readonly FileFormat Jpeg = new("image/jpeg", MediaKind.Image, ["jpg", "jpeg", "jfif"]);
    public static readonly FileFormat Png = new("image/png", MediaKind.Image, ["png"]);
    public static readonly FileFormat Gif = new("image/gif", MediaKind.Image, ["gif"]);
    public static readonly FileFormat WebP = new("image/webp", MediaKind.Image, ["webp"]);
    public static readonly FileFormat Mp4 = new("video/mp4", MediaKind.Video, ["mp4", "m4v"]);
    public static readonly FileFormat QuickTime = new("video/quicktime", MediaKind.Video, ["mov", "qt"]);
    public static readonly FileFormat WebM = new("video/webm", MediaKind.Video, ["webm"]);
    public static readonly FileFormat Mp3 = new("audio/mpeg", MediaKind.Audio, ["mp3"]);
    public static readonly FileFormat M4a = new("audio/mp4", MediaKind.Audio, ["m4a"]);
    public static readonly FileFormat Ogg = new("audio/ogg", MediaKind.Audio, ["ogg", "oga", "opus"]);
    public static readonly FileFormat Wav = new("audio/wav", MediaKind.Audio, ["wav"]);
    public static readonly FileFormat Flac = new("audio/flac", MediaKind.Audio, ["flac"]);
    public static readonly FileFormat Pdf = new("application/pdf", MediaKind.File, ["pdf"]);

    /// <summary>Every accepted format.</summary>
    public static IReadOnlyList<FileFormat> All { get; } =
        [Jpeg, Png, Gif, WebP, Mp4, QuickTime, WebM, Mp3, M4a, Ogg, Wav, Flac, Pdf];

    /// <summary>The format of a file by its first <see cref="HeaderLength"/> bytes (or all of a shorter file); <c>null</c> when none matches.</summary>
    public static FileFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return Gif;
        }

        if (header.Length >= 12 && header.StartsWith("RIFF"u8))
        {
            var form = header.Slice(8, 4);
            return form.SequenceEqual("WEBP"u8) ? WebP : form.SequenceEqual("WAVE"u8) ? Wav : null;
        }

        if (header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return IsoBrand(header.Slice(8, 4));
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x1A, 0x45, 0xDF, 0xA3]))
        {
            // Matroska and WebM share the EBML header; only the DocType "webm" is accepted.
            return header.IndexOf("webm"u8) >= 0 ? WebM : null;
        }

        if (header.StartsWith("ID3"u8) || IsMpegAudioFrame(header))
        {
            return Mp3;
        }

        if (header.StartsWith("OggS"u8))
        {
            return Ogg;
        }

        if (header.StartsWith("fLaC"u8))
        {
            return Flac;
        }

        return header.StartsWith("%PDF-"u8) ? Pdf : null;
    }

    /// <summary>Whether <paramref name="extension"/> (with or without the dot, any case) belongs to <paramref name="format"/>.</summary>
    public static bool HasExtension(FileFormat format, string extension)
    {
        ArgumentNullException.ThrowIfNull(format);
        var bare = extension.TrimStart('.');
        return format.Extensions.Any(e => string.Equals(e, bare, StringComparison.OrdinalIgnoreCase));
    }

    // The major brand of an ISO base media file (MP4, MOV, M4A): bytes 8..11 after the "ftyp" box type.
    private static FileFormat? IsoBrand(ReadOnlySpan<byte> brand) => Encoding.ASCII.GetString(brand) switch
    {
        "qt  " => QuickTime,
        "M4A " or "M4B " => M4a,
        "isom" or "iso2" or "iso3" or "iso4" or "iso5" or "iso6" or "mp41" or "mp42" or "avc1" or "M4V " or "M4VH"
            or "M4VP" or "MSNV" or "dash" or "mmp4" => Mp4,
        _ => null,
    };

    // An MPEG audio frame without an ID3 tag: 11 sync bits, then a version and a layer that are not "reserved".
    private static bool IsMpegAudioFrame(ReadOnlySpan<byte> header) =>
        header.Length >= 2
        && header[0] == 0xFF
        && (header[1] & 0xE0) == 0xE0
        && (header[1] & 0x18) != 0x08
        && (header[1] & 0x06) != 0x00;
}
