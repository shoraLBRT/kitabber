using System.Globalization;
using SkiaSharp;

namespace Platform.Modules.Media.Images;

/// <summary>
/// What a page shows in an image's place before the picture arrives (E06-03, ADR-0067): the dominant colour as
/// <c>#rrggbb</c> and a tiny WebP of the whole picture, at most <see cref="ImagePreviews.Side"/> pixels on its longer
/// side, that the page stretches and blurs.
/// </summary>
/// <param name="Color">The dominant colour: the average of the picture's pixels, weighted by their opacity.</param>
/// <param name="Image">The tiny WebP.</param>
public sealed record ImagePreviewData(string Color, byte[] Image);

/// <summary>
/// Computes an <see cref="ImagePreviewData"/> from an uploaded image (ADR-0067) with Skia. The picture is decoded at a
/// reduced scale where the format allows it (JPEG, WebP), turned upright by its EXIF orientation, like imgproxy does
/// for the delivered files, and scaled down. A file Skia cannot decode, or one too large to decode safely, has no
/// preview: the upload is not refused for it, the page shows the neutral surface instead.
/// </summary>
public static class ImagePreviews
{
    /// <summary>The longer side of the tiny preview, in pixels.</summary>
    public const int Side = 16;

    /// <summary>The most pixels decoded for a preview: a larger picture that cannot be decoded at a smaller scale is left without one.</summary>
    public const long MaxDecodedPixels = 25_000_000;

    // Decoding at about this size is enough for a 16-pixel preview and keeps the average colour accurate.
    private const int _decodeSide = 128;

    // A lossy WebP of 16 pixels at this quality is a few hundred bytes.
    private const int _quality = 60;

    /// <summary>The preview of the image in <paramref name="content"/> (read from its current position), or <c>null</c>.</summary>
    public static ImagePreviewData? Compute(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var stream = new SKManagedStream(content, disposeManagedStream: false);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            return null;
        }

        using var decoded = Decode(codec);
        if (decoded is null)
        {
            return null;
        }

        using var upright = Upright(decoded, codec.EncodedOrigin);
        var bitmap = upright ?? decoded;
        var scale = Math.Min(1.0, (double)Side / Math.Max(bitmap.Width, bitmap.Height));
        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Round(bitmap.Width * scale)),
            Math.Max(1, (int)Math.Round(bitmap.Height * scale)),
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var small = bitmap.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (small is null)
        {
            return null;
        }

        var color = AverageColor(Pixels(bitmap));
        using var image = SKImage.FromBitmap(small);
        using var webp = image.Encode(SKEncodedImageFormat.Webp, _quality);
        return webp is null ? null : new ImagePreviewData(color, webp.ToArray());
    }

    /// <summary>
    /// The average of <paramref name="pixels"/> (RGBA premultiplied by opacity, as Skia keeps them), weighted by
    /// opacity, as <c>#rrggbb</c>; a fully transparent picture is white.
    /// </summary>
    public static string AverageColor(ReadOnlySpan<byte> pixels)
    {
        // A premultiplied channel is already the colour times the opacity / 255.
        long r = 0, g = 0, b = 0, weight = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            r += pixels[i] * 255L;
            g += pixels[i + 1] * 255L;
            b += pixels[i + 2] * 255L;
            weight += pixels[i + 3];
        }

        return weight == 0
            ? "#ffffff"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"#{Channel(r, weight):x2}{Channel(g, weight):x2}{Channel(b, weight):x2}");
    }

    private static int Channel(long sum, long weight) => Math.Clamp((int)Math.Round((double)sum / weight), 0, 255);

    // The picture at about _decodeSide pixels where the codec can scale while decoding, else at full size.
    private static SKBitmap? Decode(SKCodec codec)
    {
        var size = codec.Info.Size;
        var scaled = codec.GetScaledDimensions(Math.Min(1f, (float)_decodeSide / Math.Max(size.Width, size.Height)));
        foreach (var dimensions in new[] { scaled, size }.Distinct())
        {
            if ((long)dimensions.Width * dimensions.Height > MaxDecodedPixels)
            {
                continue;
            }

            var info = new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(info, bitmap.GetPixels());
            if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
            {
                return bitmap;
            }

            bitmap.Dispose();
        }

        return null;
    }

    // The picture turned by its EXIF orientation, or null when it is already upright.
    private static SKBitmap? Upright(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return null;
        }

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int w = bitmap.Width, h = bitmap.Height;
        var result = new SKBitmap(new SKImageInfo(swap ? h : w, swap ? w : h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            _ => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1), // LeftBottom
        });
        canvas.DrawBitmap(bitmap, 0, 0);
        return result;
    }

    private static ReadOnlySpan<byte> Pixels(SKBitmap bitmap) => bitmap.GetPixelSpan();
}
