using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>
/// Real pictures for imgproxy, which decodes them (<see cref="SampleFiles"/> only start like a format), and the size
/// of what it sends back, read from the WebP or AVIF header.
/// </summary>
public static class Pictures
{
    /// <summary>A PNG of <paramref name="width"/> by <paramref name="height"/> opaque pixels in a gradient.</summary>
    public static byte[] Png(int width, int height)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // no filter
            for (var x = 0; x < width; x++)
            {
                raw.Write([(byte)(x * 255 / width), (byte)(y * 255 / height), 128]);
            }
        }

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bits per channel
        header[9] = 2; // RGB
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", idat.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>The width and height of a WebP (lossy, lossless or extended) or an AVIF (its <c>ispe</c> box).</summary>
    public static (int Width, int Height) SizeOf(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (Encoding.ASCII.GetString(image, 8, 4) == "WEBP")
        {
            return Encoding.ASCII.GetString(image, 12, 4) switch
            {
                "VP8 " => (BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(26)) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(28)) & 0x3FFF),
                "VP8L" => (1 + (((image[22] & 0x3F) << 8) | image[21]), 1 + (((image[24] & 0x0F) << 10) | (image[23] << 2) | ((image[22] & 0xC0) >> 6))),
                "VP8X" => (1 + (image[24] | (image[25] << 8) | (image[26] << 16)), 1 + (image[27] | (image[28] << 8) | (image[29] << 16))),
                var chunk => throw new InvalidDataException($"Unknown WebP chunk '{chunk}'."),
            };
        }

        var ispe = image.AsSpan().IndexOf("ispe"u8);
        return Encoding.ASCII.GetString(image, 4, 8) == "ftypavif" && ispe > 0
            ? (BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(ispe + 8)), BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(ispe + 12)))
            : throw new InvalidDataException("Neither a WebP nor an AVIF.");
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        png.Write(typeBytes);
        png.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32([.. typeBytes, .. data]));
        png.Write(number);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
