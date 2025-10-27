using System.Net;
using System.Runtime.CompilerServices;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.Svc.BlobStorage.AwsS3;

public sealed class S3BlobService(IOptions<S3Options> options, ILogger<S3BlobService> logger)
    : BlobService(options.Value), IDisposable
{
    #region Fields

    private AmazonS3Client? _client;
    private readonly S3Options _options = options.Value;

    #endregion

    #region Methods

    public override async Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        var client = await GetS3ClientAsync(cancellationToken);

        try
        {
            var response = await client.ListObjectsAsync(new ListObjectsRequest
            {
                BucketName = _options.BucketName,
                Prefix = location,
                //Delimiter = "/",
                MaxKeys = 2
            }, cancellationToken);

            return response.S3Objects is not null
                   && response.S3Objects.Exists(s => s.Key.Equals(location, StringComparison.OrdinalIgnoreCase));
        }
        catch (AmazonS3Exception e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
                return false;
            throw;
        }
    }

    public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        return blob.Type == BlobTypes.File
            ? DeleteFileAsync(location, cancellationToken)
            : DeleteFolderAsync(location, cancellationToken);
    }

    private async Task<bool> DeleteFileAsync(string fileLocation, CancellationToken cancellationToken = default)
    {
        var client = await GetS3ClientAsync(cancellationToken);

        var response =
            await client.DeleteObjectAsync(_options.BucketName, fileLocation, cancellationToken);
        return response.HttpStatusCode == HttpStatusCode.NoContent;
    }

    private async Task<bool> DeleteFolderAsync(string folderLocation, CancellationToken cancellationToken = default)
    {
        var client = await GetS3ClientAsync(cancellationToken);
        do
        {
            var info = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = folderLocation
            }, cancellationToken);

            if (info?.S3Objects is null || info.S3Objects.Count == 0)
                break;

            foreach (var obj in info.S3Objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await client.DeleteObjectAsync(obj.BucketName, obj.Key, cancellationToken);
            }
        } while (true);

        await client.DeleteObjectAsync(_options.BucketName, folderLocation, cancellationToken);
        return true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // The bulk of the clean-up code is implemented in Dispose(bool)
    private void Dispose(bool disposing)
    {
        if (!disposing || _client is null) return;
        _client.Dispose();
        _client = null;
    }

    public override async Task<BlobDataResult?> GetAsync(BlobRequest blob,
        CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        var client = await GetS3ClientAsync(cancellationToken);
        try
        {
            var info = await client.GetObjectAsync(_options.BucketName, location, cancellationToken);
            var data = await BinaryData.FromStreamAsync(info.ResponseStream, cancellationToken);

            return new BlobDataResult(blob.Name, data)
            {
                Type = BlobTypes.File,
                Details = new BlobDetails
                {
                    ContentType = info.Headers.ContentType,
                    ContentLength = info.ContentLength,
                    CreatedOn = info.LastModified!.Value,
                    LastModified = info.LastModified!.Value
                }
            };
        }
        catch (AmazonS3Exception e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
                return null;
            throw;
        }
    }

    public override async Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        var client = await GetS3ClientAsync(cancellationToken);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = location,
            Expires = DateTime.UtcNow.Add(expiresFromNow ?? TimeSpan.FromHours(1))
        };

        var uri = await client.GetPreSignedURLAsync(request);

        return uri is null
            ? throw new NotSupportedException(
                $"Current Container '{_options.BucketName}' does not support Shared Public Access Url")
            : new Uri(uri);
    }

    private async Task<AmazonS3Client> GetS3ClientAsync(CancellationToken cancellationToken = default)
    {
        if (_client != null) return _client;

        var config = new AmazonS3Config
        {
            ServiceURL = _options.ConnectionString,
            ForcePathStyle = _options.ForcePathStyle,
            UseHttp = !_options.ConnectionString.StartsWith("https", StringComparison.CurrentCultureIgnoreCase),
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        if (!string.IsNullOrWhiteSpace(_options.AccessKey) && !string.IsNullOrWhiteSpace(_options.Secret))
        {
            _client = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKey, _options.Secret), config);
            logger.LogInformation("Loaded AmazonS3Client with BasicAWSCredentials");
        }
        else
        {
            _client = new AmazonS3Client(config);
            logger.LogInformation("Loaded AmazonS3Client without Credentials");
        }

        //Create Bucket if not exists
        var buckets = await _client.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets is null || !buckets.Buckets.Exists(b =>
                b.BucketName.Equals(_options.BucketName, StringComparison.OrdinalIgnoreCase)))
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _options.BucketName }, cancellationToken);

        return _client;
    }

    public override async IAsyncEnumerable<BlobResult> ListItemsAsync(BlobRequest blob,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        var client = await GetS3ClientAsync(cancellationToken);

        var info = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = _options.BucketName,
            Prefix = location
        }, cancellationToken);

        if (info?.S3Objects is null) yield break;

        foreach (var obj in info.S3Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new BlobResult(obj.Key)
            {
                Type = obj.Size > 1 ? BlobTypes.File : BlobTypes.Directory,
                Details = obj.Size > 1
                    ? new BlobDetails
                    {
                        ContentType = string.Empty,
                        ContentLength = obj.Size.Value,
                        CreatedOn = obj.LastModified!.Value,
                        LastModified = obj.LastModified!.Value
                    }
                    : null
            };
        }
    }

    public override async Task<string> SaveAsync(BlobData blob, CancellationToken cancellationToken = default)
    {
        var existed = await CheckExistsAsync(blob, cancellationToken);
        if (existed && !blob.Overwrite)
            throw new InvalidOperationException($"File {blob.Name} is not allowed to override.");

        var location = GetBlobLocation(blob);
        var client = await GetS3ClientAsync(cancellationToken);

        var uploadRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = location,
            InputStream = blob.Data.ToStream(),
            ContentType = blob.ContentType,
            DisablePayloadSigning = _options.DisablePayloadSigning
        };

        await client.PutObjectAsync(uploadRequest, cancellationToken);
        return blob.Name;
    }

    #endregion
}