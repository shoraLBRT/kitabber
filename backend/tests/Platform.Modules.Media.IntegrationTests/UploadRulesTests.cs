using Microsoft.Extensions.Configuration;
using Platform.Modules.Media.Storage;
using Platform.Modules.Media.Uploads;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>E06-01: recognition by content, the extension rule, the size limits and the storage keys, without Docker.</summary>
public sealed class UploadRulesTests
{
    public static TheoryData<string, string, MediaKind> Formats => new()
    {
        { nameof(SampleFiles.Jpeg), "image/jpeg", MediaKind.Image },
        { nameof(SampleFiles.Png), "image/png", MediaKind.Image },
        { nameof(SampleFiles.Gif), "image/gif", MediaKind.Image },
        { nameof(SampleFiles.WebP), "image/webp", MediaKind.Image },
        { nameof(SampleFiles.Mp4), "video/mp4", MediaKind.Video },
        { nameof(SampleFiles.QuickTime), "video/quicktime", MediaKind.Video },
        { nameof(SampleFiles.WebM), "video/webm", MediaKind.Video },
        { nameof(SampleFiles.Mp3), "audio/mpeg", MediaKind.Audio },
        { nameof(SampleFiles.Mp3WithoutTag), "audio/mpeg", MediaKind.Audio },
        { nameof(SampleFiles.M4a), "audio/mp4", MediaKind.Audio },
        { nameof(SampleFiles.Ogg), "audio/ogg", MediaKind.Audio },
        { nameof(SampleFiles.Wav), "audio/wav", MediaKind.Audio },
        { nameof(SampleFiles.Flac), "audio/flac", MediaKind.Audio },
        { nameof(SampleFiles.Pdf), "application/pdf", MediaKind.File },
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void FormatIsRecognisedByItsBytes(string sample, string contentType, MediaKind kind)
    {
        var format = FileFormats.Detect(Sample(sample));

        Assert.NotNull(format);
        Assert.Equal(contentType, format.ContentType);
        Assert.Equal(kind, format.Kind);
    }

    [Theory]
    [InlineData(nameof(SampleFiles.Executable))]
    [InlineData(nameof(SampleFiles.Html))]
    [InlineData(nameof(SampleFiles.Matroska))]
    public void UnknownContentMatchesNoFormat(string sample) => Assert.Null(FileFormats.Detect(Sample(sample)));

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.JPEG")]
    [InlineData("photo")]
    [InlineData(null)]
    public void FileWhoseNameAgreesWithItsContentIsAccepted(string? name)
    {
        var check = Check(name, SampleFiles.Jpeg());

        Assert.True(check.Accepted);
        Assert.Equal(FileFormats.Jpeg, check.Format);
    }

    [Theory]
    [InlineData("virus.jpg", nameof(SampleFiles.Executable), UploadRefusal.UnsupportedType)]
    [InlineData("page.png", nameof(SampleFiles.Html), UploadRefusal.UnsupportedType)]
    [InlineData("photo.jpg", nameof(SampleFiles.Png), UploadRefusal.ExtensionMismatch)]
    [InlineData("clip.mp4", nameof(SampleFiles.Pdf), UploadRefusal.ExtensionMismatch)]
    [InlineData("song.mp3", nameof(SampleFiles.Wav), UploadRefusal.ExtensionMismatch)]
    public void FileWithASwappedExtensionIsRefused(string name, string sample, UploadRefusal refusal)
    {
        var check = Check(name, Sample(sample));

        Assert.False(check.Accepted);
        Assert.Equal(refusal, check.Refusal);
    }

    [Fact]
    public void EmptyFileIsRefused() => Assert.Equal(UploadRefusal.Empty, Check("a.png", []).Refusal);

    [Fact]
    public void EachKindHasItsOwnLimit()
    {
        var limits = new MediaLimits(Image: 1000, Video: 3000, Audio: 2000, File: 500);

        Assert.True(UploadRules.Check("a.png", SampleFiles.Png(), 1000, limits).Accepted);
        var image = UploadRules.Check("a.png", SampleFiles.Png(), 1001, limits);
        Assert.Equal(UploadRefusal.TooLarge, image.Refusal);
        Assert.Equal(1000, image.Limit);
        Assert.True(UploadRules.Check("a.mp4", SampleFiles.Mp4(), 3000, limits).Accepted);
        Assert.Equal(UploadRefusal.TooLarge, UploadRules.Check("a.wav", SampleFiles.Wav(), 2001, limits).Refusal);
        Assert.Equal(UploadRefusal.TooLarge, UploadRules.Check("a.pdf", SampleFiles.Pdf(), 501, limits).Refusal);
        Assert.Equal(3000, limits.Largest);
    }

    [Fact]
    public void LimitsComeFromTheSettings()
    {
        var limits = MediaSettings.ReadLimits(Settings(new()
        {
            [MediaSettings.ImageMaxBytesKey] = "1048576",
            [MediaSettings.FileMaxBytesKey] = "2048",
        }));

        Assert.Equal(new MediaLimits(1048576, MediaLimits.Default.Video, MediaLimits.Default.Audio, 2048), limits);
        Assert.Equal(MediaLimits.Default, MediaSettings.ReadLimits(Settings([])));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("20MB")]
    public void LimitThatIsNotAPositiveNumberOfBytesStopsTheHost(string value) =>
        Assert.Throws<InvalidOperationException>(
            () => MediaSettings.ReadLimits(Settings(new() { [MediaSettings.VideoMaxBytesKey] = value })));

    [Fact]
    public void StorageKeysAreRandomAndCarryNothingElse()
    {
        var keys = Enumerable.Range(0, 1000).Select(_ => StorageKeys.NewOriginal()).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.Matches("^originals/[0-9a-f]{32}$", key));
        // Not ordered by time like a UUID v7: neighbours share no common prefix beyond chance.
        var sharedPrefixes = keys.Zip(keys.Skip(1)).Count(pair => pair.First[..14] == pair.Second[..14]);
        Assert.True(sharedPrefixes < 50, $"{sharedPrefixes} neighbouring keys share their first digits");
    }

    [Fact]
    public void FileKeepsItsOwnerAndOnlyTheNameOfTheOriginal()
    {
        var owner = Guid.NewGuid();

        var file = MediaFile.Create(owner, FileFormats.Png, 10, "originals/k", @"C:\Users\me\..\photo.png", DateTimeOffset.UnixEpoch);

        Assert.Equal(owner, file.OwnerAccountId);
        Assert.Equal("photo.png", file.FileName);
        Assert.Throws<ArgumentException>(
            () => MediaFile.Create(Guid.Empty, FileFormats.Png, 10, "originals/k", null, DateTimeOffset.UnixEpoch));
    }

    private static UploadCheck Check(string? name, byte[] bytes) =>
        UploadRules.Check(name, bytes.AsSpan(0, Math.Min(bytes.Length, FileFormats.HeaderLength)), bytes.Length, MediaLimits.Default);

    private static byte[] Sample(string name) =>
        (byte[])typeof(SampleFiles).GetMethod(name)!.Invoke(null, [256])!;

    private static IConfiguration Settings(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
