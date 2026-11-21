using System.Text;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>Files of each format, made of their real signatures and filler: enough for recognition by content.</summary>
public static class SampleFiles
{
    public static byte[] Jpeg(int size = 256) => Pad([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, .. "JFIF"u8, 0x00], size);

    public static byte[] Png(int size = 256) => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, .. "IHDR"u8], size);

    public static byte[] Gif(int size = 256) => Pad("GIF89a"u8.ToArray(), size);

    public static byte[] WebP(int size = 256) => Pad([.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8], size);

    public static byte[] Mp4(int size = 256) => Pad([0x00, 0x00, 0x00, 0x20, .. "ftypisom"u8, 0x00, 0x00, 0x02, 0x00, .. "isomiso2mp41"u8], size);

    public static byte[] QuickTime(int size = 256) => Pad([0x00, 0x00, 0x00, 0x14, .. "ftypqt  "u8, 0x00, 0x00, 0x00, 0x00, .. "qt  "u8], size);

    public static byte[] WebM(int size = 256) =>
        Pad([0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0x82, 0x84, .. "webm"u8], size);

    public static byte[] Matroska(int size = 256) =>
        Pad([0x1A, 0x45, 0xDF, 0xA3, 0xA3, 0x42, 0x86, 0x81, 0x01, 0x42, 0x82, 0x88, .. "matroska"u8], size);

    public static byte[] Mp3(int size = 256) => Pad([.. "ID3"u8, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], size);

    public static byte[] Mp3WithoutTag(int size = 256) => Pad([0xFF, 0xFB, 0x90, 0x64], size);

    public static byte[] M4a(int size = 256) => Pad([0x00, 0x00, 0x00, 0x20, .. "ftypM4A "u8, 0x00, 0x00, 0x00, 0x00], size);

    public static byte[] Ogg(int size = 256) => Pad([.. "OggS"u8, 0x00, 0x02], size);

    public static byte[] Wav(int size = 256) => Pad([.. "RIFF"u8, 0x24, 0x08, 0x00, 0x00, .. "WAVEfmt "u8], size);

    public static byte[] Flac(int size = 256) => Pad([.. "fLaC"u8, 0x00, 0x00, 0x00, 0x22], size);

    public static byte[] Pdf(int size = 256) => Pad([.. "%PDF-1.7\n"u8], size);

    /// <summary>A Windows program: what a renamed malicious "photo" usually is.</summary>
    public static byte[] Executable(int size = 256) => Pad([.. "MZ"u8, 0x90, 0x00, 0x03, 0x00], size);

    /// <summary>A web page with a script: never served back as an image.</summary>
    public static byte[] Html(int size = 256) => Pad(Encoding.UTF8.GetBytes("<html><script>alert(1)</script>"), size);

    private static byte[] Pad(byte[] signature, int size)
    {
        var bytes = new byte[Math.Max(size, signature.Length)];
        signature.CopyTo(bytes, 0);
        for (var i = signature.Length; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i * 31);
        }

        return bytes;
    }
}
