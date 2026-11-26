using Platform.Modules.Media.Images;
using SkiaSharp;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>E06-03 (ADR-0067): the blurred preview and the dominant colour computed from an uploaded image.</summary>
public sealed class ImagePreviewsTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void SolidPictureGivesItsColourAndATinyWebPOfItsProportions(SKEncodedImageFormat format)
    {
        var picture = Solid(400, 300, new SKColor(0x20, 0x80, 0xc0), format);

        var preview = ImagePreviews.Compute(new MemoryStream(picture));

        Assert.NotNull(preview);
        // Lossy formats may move a channel by a step or two.
        AssertClose("#2080c0", preview.Color, format == SKEncodedImageFormat.Png ? 0 : 3);
        Assert.Equal((16, 12), Pictures.SizeOf(preview.Image));
        Assert.True(preview.Image.Length < 1024, $"The preview is {preview.Image.Length} bytes.");
    }

    [Fact]
    public void PortraitKeepsItsProportionsAndASmallPictureIsNotEnlarged()
    {
        var portrait = ImagePreviews.Compute(new MemoryStream(Solid(300, 600, SKColors.Black, SKEncodedImageFormat.Png)));
        var small = ImagePreviews.Compute(new MemoryStream(Solid(4, 2, SKColors.White, SKEncodedImageFormat.Png)));

        Assert.Equal((8, 16), Pictures.SizeOf(portrait!.Image));
        Assert.Equal("#000000", portrait.Color);
        Assert.Equal((4, 2), Pictures.SizeOf(small!.Image));
        Assert.Equal("#ffffff", small.Color);
    }

    [Fact]
    public void ColourIsTheAverageOfTheVisiblePixels()
    {
        // Half red, half transparent: the transparent half does not darken the colour.
        using var bitmap = new SKBitmap(new SKImageInfo(20, 10, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, 10, 10, paint);
        }

        var preview = ImagePreviews.Compute(new MemoryStream(Encode(bitmap, SKEncodedImageFormat.Png)));

        Assert.Equal("#ff0000", preview!.Color);
        Assert.Equal("#ffffff", ImagePreviews.AverageColor([0, 0, 0, 0]));
        // Premultiplied: half-opaque white and opaque black average to a weighted grey.
        Assert.Equal("#555555", ImagePreviews.AverageColor([128, 128, 128, 128, 0, 0, 0, 255]));
    }

    [Fact]
    public void OnePixelPictureGivesAOnePixelPreview()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

        var preview = ImagePreviews.Compute(new MemoryStream(png));

        Assert.Equal((1, 1), Pictures.SizeOf(preview!.Image));
    }

    [Fact]
    public void GradientGivesItsMiddleColour()
    {
        var preview = ImagePreviews.Compute(new MemoryStream(Pictures.Png(64, 48)));

        AssertClose("#7d7d80", preview!.Color, 6);
    }

    [Theory]
    [InlineData(nameof(SampleFiles.Png))]
    [InlineData(nameof(SampleFiles.Jpeg))]
    [InlineData(nameof(SampleFiles.Pdf))]
    public void FileSkiaCannotDecodeHasNoPreview(string sample)
    {
        // The samples only start like their format.
        var bytes = (byte[])typeof(SampleFiles).GetMethod(sample)!.Invoke(null, [512])!;

        Assert.Null(ImagePreviews.Compute(new MemoryStream(bytes)));
    }

    private static byte[] Solid(int width, int height, SKColor color, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(color);
        return Encode(bitmap, format);
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }

    private static void AssertClose(string expected, string actual, int tolerance)
    {
        Assert.Matches("^#[0-9a-f]{6}$", actual);
        for (var i = 1; i < 7; i += 2)
        {
            var e = Convert.ToInt32(expected.Substring(i, 2), 16);
            var a = Convert.ToInt32(actual.Substring(i, 2), 16);
            Assert.True(Math.Abs(e - a) <= tolerance, $"Expected about {expected}, got {actual}.");
        }
    }
}
