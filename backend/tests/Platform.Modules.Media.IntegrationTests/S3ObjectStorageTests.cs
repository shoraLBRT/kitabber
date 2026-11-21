using Platform.Modules.Media.Storage;

namespace Platform.Modules.Media.IntegrationTests;

/// <summary>E06-01: <see cref="S3ObjectStorage"/> on MinIO (the storage of compose.yaml) in Testcontainers.</summary>
public sealed class S3ObjectStorageTests(MinioFixture minio)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OriginalIsSavedReadAndRemovedInABucketCreatedOnFirstUse()
    {
        using var storage = Create($"media-{Guid.NewGuid():N}");
        var key = StorageKeys.NewOriginal();
        var bytes = SampleFiles.Png(70_000);

        Assert.Null(await storage.FindAsync(key, Ct));
        using (var content = new MemoryStream(bytes))
        {
            await storage.PutAsync(key, content, "image/png", Ct);
        }

        Assert.Equal(new StoredObject(key, "image/png", bytes.Length), await storage.FindAsync(key, Ct));
        await using (var read = (await storage.OpenReadAsync(key, Ct))!)
        {
            using var copy = new MemoryStream();
            await read.CopyToAsync(copy, Ct);
            Assert.Equal(bytes, copy.ToArray());
        }

        await storage.DeleteAsync(key, Ct);
        await storage.DeleteAsync(key, Ct);
        Assert.Null(await storage.FindAsync(key, Ct));
        Assert.Null(await storage.OpenReadAsync(key, Ct));
    }

    [Fact]
    public async Task SecondStorageOnTheSameBucketFindsItReady()
    {
        var bucket = $"media-{Guid.NewGuid():N}";
        using var first = Create(bucket);
        using var second = Create(bucket);
        var key = StorageKeys.NewOriginal();

        await first.PutAsync(key, new MemoryStream(SampleFiles.Pdf()), "application/pdf", Ct);
        await second.PutAsync(StorageKeys.NewOriginal(), new MemoryStream(SampleFiles.Pdf()), "application/pdf", Ct);

        Assert.Equal("application/pdf", (await second.FindAsync(key, Ct))?.ContentType);
    }

    private S3ObjectStorage Create(string bucket) => new(new S3StorageOptions(
        minio.ServiceUrl, MediaSettings.DefaultRegion, bucket, MinioFixture.AccessKey, MinioFixture.SecretKey, CreateBucket: true));
}
