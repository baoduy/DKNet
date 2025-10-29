// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: S3BlobService.cs
// Description: Amazon S3 implementation of the BlobService abstraction (AWS S3 compatible storage).

using System.Net;
using System.Runtime.CompilerServices;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.Svc.BlobStorage.AwsS3;

/// <summary>
///     Amazon S3 compatible implementation of <see cref="BlobService" />. Provides file and folder operations
///     (check, list, save, delete, and URL generation) against an S3 bucket.
/// </summary>
/// <param name="options">The configured <see cref="S3Options" />.</param>
/// <param name="logger">Logger instance for diagnostic output.</param>
public sealed class S3BlobService(IOptions<S3Options> options, ILogger<S3BlobService> logger)
    : BlobService(options.Value), IDisposable
{
    #region Fields

    private readonly S3Options _options = options.Value;

    private AmazonS3Client? _client;

    #endregion

    #region Methods

    /// <summary>
    ///     Checks whether a blob exists at the computed storage location for the provided <paramref name="blob" />.
    /// </summary>
    /// <param name="blob">Blob request describing the item to check.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns><c>true</c> when a matching object exists; otherwise <c>false</c>.</returns>
    public override async Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        var client = await GetS3ClientAsync(cancellationToken);

        try
        {
            var response = await client.ListObjectsAsync(
                new ListObjectsRequest
                {
                    BucketName = _options.BucketName,
                    Prefix = location,

                    //Delimiter = "/",
                    MaxKeys = 2
                },
                cancellationToken);

            return response.S3Objects is not null
                   && response.S3Objects.Exists(s => s.Key.Equals(location, StringComparison.OrdinalIgnoreCase));
        }
        catch (AmazonS3Exception e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound) return false;

            throw;
        }
    }

    /// <summary>
    ///     Deletes a blob or folder represented by the provided <paramref name="blob" /> request.
    ///     For folders this implementation enumerates and deletes contained objects recursively.
    /// </summary>
    /// <param name="blob">Blob request describing the target to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns><c>true</c> when deletion completed (or object did not exist); otherwise <c>false</c>.</returns>
    public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        return blob.Type == BlobTypes.File
            ? DeleteFileAsync(location, cancellationToken)
            : DeleteFolderAsync(location, cancellationToken);
    }

    /// <summary>
    ///     Deletes a single object at the specified S3 key location.
    /// </summary>
    /// <param name="fileLocation">The normalized S3 key to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns><c>true</c> when deletion returns NoContent; otherwise <c>false</c>.</returns>
    private async Task<bool> DeleteFileAsync(string fileLocation, CancellationToken cancellationToken = default)
    {
        var client = await GetS3ClientAsync(cancellationToken);

        var response =
            await client.DeleteObjectAsync(_options.BucketName, fileLocation, cancellationToken);
        return response.HttpStatusCode == HttpStatusCode.NoContent;
    }

    /// <summary>
    ///     Deletes all objects under the given prefix (folder-like behavior) and then attempts to delete the prefix object.
    /// </summary>
    /// <param name="folderLocation">The prefix used to list and delete contained objects.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns><c>true</c> when deletion completes; otherwise may throw.</returns>
    private async Task<bool> DeleteFolderAsync(string folderLocation, CancellationToken cancellationToken = default)
    {
        var client = await GetS3ClientAsync(cancellationToken);
        do
        {
            var info = await client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _options.BucketName,
                    Prefix = folderLocation
                },
                cancellationToken);

            if (info?.S3Objects is null || info.S3Objects.Count == 0) break;

            foreach (var obj in info.S3Objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await client.DeleteObjectAsync(obj.BucketName, obj.Key, cancellationToken);
            }
        } while (true);

        await client.DeleteObjectAsync(_options.BucketName, folderLocation, cancellationToken);
        return true;
    }

    /// <summary>
    ///     Disposes managed resources held by this instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
    }

    // The bulk of the clean-up code is implemented in Dispose(bool)
    private void Dispose(bool disposing)
    {
        if (!disposing || _client is null) return;

        _client.Dispose();
        _client = null;
    }

    /// <summary>
    ///     Retrieves the blob content and metadata for the provided <paramref name="blob" /> request.
    /// </summary>
    /// <param name="blob">Blob request describing which object to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A BlobDataResult containing the content and metadata when found; otherwise <c>null</c>.</returns>
    public override async Task<BlobDetails.BlobDataResult?> GetAsync(
        BlobRequest blob,
        CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        var client = await GetS3ClientAsync(cancellationToken);
        try
        {
            var info = await client.GetObjectAsync(_options.BucketName, location, cancellationToken);
            var data = await BinaryData.FromStreamAsync(info.ResponseStream, cancellationToken);

            return new BlobDetails.BlobDataResult(blob.Name, data)
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
            if (e.StatusCode == HttpStatusCode.NotFound) return null;

            throw;
        }
    }

    /// <summary>
    ///     Generates a pre-signed public access URL for the specified blob when supported by the S3 bucket.
    /// </summary>
    /// <param name="blob">Blob request describing the object to generate a URL for.</param>
    /// <param name="expiresFromNow">Optional expiry window for the pre-signed URL.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A <see cref="Uri" /> granting temporary read access to the object.</returns>
    public override async Task<Uri> GetPublicAccessUrl(
        BlobRequest blob,
        TimeSpan? expiresFromNow = null,
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

    /// <summary>
    ///     Creates or returns a cached <see cref="AmazonS3Client" /> configured from <see cref="S3Options" />.
    ///     The client will attempt to create the configured bucket if it does not already exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>An initialized <see cref="AmazonS3Client" /> instance.</returns>
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
            _client = new AmazonS3Client(
                new BasicAWSCredentials(_options.AccessKey, _options.Secret),
                config);
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
            await _client.PutBucketAsync(
                new PutBucketRequest { BucketName = _options.BucketName },
                cancellationToken);

        return _client;
    }

    /// <summary>
    ///     Lists objects under the provided prefix and yields blob metadata results.
    /// </summary>
    /// <param name="blob">Blob request describing the prefix to list.</param>
    /// <param name="cancellationToken">Cancellation token for the async enumeration.</param>
    /// <returns>An async stream of <see cref="BlobDetails.BlobResult" /> entries.</returns>
    public override async IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(
        BlobRequest blob,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        var client = await GetS3ClientAsync(cancellationToken);

        var info = await client.ListObjectsV2Async(
            new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = location
            },
            cancellationToken);

        if (info?.S3Objects is null) yield break;

        foreach (var obj in info.S3Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new BlobDetails.BlobResult(obj.Key)
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

    /// <summary>
    ///     Uploads the provided blob payload to S3 and returns the saved blob name.
    /// </summary>
    /// <param name="blob">The blob payload and metadata to upload.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>The original blob name when the upload completes.</returns>
    public override async Task<string> SaveAsync(BlobDetails.BlobData blob,
        CancellationToken cancellationToken = default)
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