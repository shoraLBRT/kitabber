using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Platform.Modules.Media.Storage;

/// <summary>The S3 bucket of media and how to reach it (<c>Media:Storage:*</c>, docs/CONFIG.md).</summary>
/// <param name="ServiceUrl">The S3 endpoint, for example <c>http://localhost:9000</c> for MinIO.</param>
/// <param name="Region">The signing region; MinIO accepts any, <c>us-east-1</c> by default.</param>
/// <param name="Bucket">The bucket of media.</param>
/// <param name="AccessKey">The access key id.</param>
/// <param name="SecretKey">The secret access key.</param>
/// <param name="CreateBucket">Whether to create the bucket when it is missing: for MinIO on a developer's machine.</param>
public sealed record S3StorageOptions(
    Uri ServiceUrl,
    string Region,
    string Bucket,
    string AccessKey,
    string SecretKey,
    bool CreateBucket);

/// <summary>
/// <see cref="IObjectStorage"/> over the S3 API (ADR-0030): any S3-compatible service, MinIO locally. Path-style
/// addresses, so the bucket needs no DNS name of its own; checksums only where the API requires them, which every
/// S3-compatible service understands.
/// </summary>
public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly S3StorageOptions _options;
    private readonly SemaphoreSlim _bucket = new(1, 1);
    private bool _bucketReady;

    public S3ObjectStorage(S3StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl.ToString(),
                AuthenticationRegion = options.Region,
                ForcePathStyle = true,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
        _bucketReady = !options.CreateBucket;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(content);
        await EnsureBucketAsync(cancellationToken);
        await _client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken);
    }

    public async Task<StoredObject?> FindAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _client.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);
            return new StoredObject(key, metadata.Headers.ContentType, metadata.ContentLength);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectAsync(_options.Bucket, key, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await _client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);

    public void Dispose()
    {
        _client.Dispose();
        _bucket.Dispose();
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _bucketReady))
        {
            return;
        }

        await _bucket.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
            {
                return;
            }

            try
            {
                await _client.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
            }
            catch (AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // Created before, by this process or another one.
            }

            Volatile.Write(ref _bucketReady, true);
        }
        finally
        {
            _bucket.Release();
        }
    }
}
