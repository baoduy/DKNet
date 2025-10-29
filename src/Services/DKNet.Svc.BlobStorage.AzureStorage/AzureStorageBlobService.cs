// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AzureStorageBlobService.cs
// Description: Azure Blob Storage implementation of the BlobService abstraction.

namespace DKNet.Svc.BlobStorage.AzureStorage;

/// <summary>
///     Azure Blob Storage provider implementing <see cref="BlobService" /> using
///     <c>Azure.Storage.Blobs</c> clients.
/// </summary>
/// <param name="options">The options wrapper that provides <see cref="AzureStorageOptions" />.</param>
public sealed class AzureStorageBlobService(IOptions<AzureStorageOptions> options) : BlobService(options.Value)
{
    #region Fields

    private readonly AzureStorageOptions _options =
        options.Value ?? throw new ArgumentNullException(nameof(options));

    private BlobContainerClient? _containerClient;

    #endregion

    #region Methods

    /// <summary>
    ///     Checks whether the specified blob exists in the configured Azure container.
    /// </summary>
    /// <param name="blob">The blob request describing the container name and blob path.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>True when the blob exists; otherwise false.</returns>
    public override async Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob);
        var rs = await client.GetBlobClient(location).ExistsAsync(cancellationToken);
        return rs.Value;
    }

    /// <summary>
    ///     Deletes a blob or folder identified by the provided <paramref name="blob" /> request.
    ///     If the blob request represents a folder, the implementation will enumerate and delete contained items.
    /// </summary>
    /// <param name="blob">The blob request describing the blob or folder to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>True when the delete completed successfully; otherwise false.</returns>
    public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var location = GetBlobLocation(blob);
        return blob.Type == BlobTypes.File
            ? DeleteFileAsync(location, cancellationToken)
            : DeleteFolderAsync(location, cancellationToken);
    }

    /// <summary>
    ///     Deletes a single file blob at the specified location.
    /// </summary>
    /// <param name="fileLocation">Normalized blob path to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>True when the delete operation succeeded or the blob did not exist.</returns>
    private async Task<bool> DeleteFileAsync(string fileLocation, CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var rs = await client.GetBlobClient(fileLocation).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return rs.Value;
    }

    /// <summary>
    ///     Deletes a folder and all contained blobs by enumerating child items recursively.
    /// </summary>
    /// <param name="folderLocation">The folder path (with trailing slash) to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>True when deletion succeeds; otherwise may throw an exception.</returns>
    private async Task<bool> DeleteFolderAsync(string folderLocation, CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var queue = new Queue<string>();
        var subStack = new Stack<string>();
        queue.Enqueue(folderLocation.EnsureTrailingSlash());

        while (queue.Count > 0)
        {
            var tbDelete = queue.Dequeue();
            var resultSegment = client.GetBlobsAsync(BlobTraits.None, BlobStates.All, tbDelete, cancellationToken);

            //Delete Files
            await foreach (var blob in resultSegment)
                if (blob.IsDirectory())
                    queue.Enqueue(blob.Name.EnsureTrailingSlash());
                else
                    await DeleteFileAsync(blob.Name, cancellationToken);

            //Add Empty folder to stack and delete later
            subStack.Push(tbDelete);
        }

        //Delete all empty Subfolders and folder
        while (subStack.Count > 0) await DeleteFileAsync(subStack.Pop(), cancellationToken);

        //Tobe True or Exception.
        return true;
    }

    /// <summary>
    ///     Retrieves blob content and metadata for the specified blob request.
    /// </summary>
    /// <param name="blob">The blob request describing the blob to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A BlobDataResult containing content and details when found; otherwise null.</returns>
    public override async Task<BlobDetails.BlobDataResult?> GetAsync(
        BlobRequest blob,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob);
        var b = client.GetBlobClient(location);
        var props = await b.GetPropertiesAsync(cancellationToken: cancellationToken);
        var es = await b.ExistsAsync(cancellationToken);
        if (!es.Value) return null;

        var data = await b.DownloadContentAsync(cancellationToken);
        return new BlobDetails.BlobDataResult(blob.Name, data.Value.Content)
        {
            Type = BlobTypes.File,
            Details = new BlobDetails
            {
                ContentType = props.Value.ContentType,
                ContentLength = props.Value.ContentLength,
                CreatedOn = props.Value.CreatedOn.LocalDateTime,
                LastModified = props.Value.LastModified.LocalDateTime
            }
        };
    }

    /// <summary>
    ///     Creates or returns a cached <see cref="BlobContainerClient" /> for the configured container.
    /// </summary>
    /// <returns>An initialized <see cref="BlobContainerClient" /> instance.</returns>
    private async Task<BlobContainerClient> GetClient()
    {
        if (_containerClient != null) return _containerClient;

        var blobClient = new BlobServiceClient(_options.ConnectionString);
        _containerClient = blobClient.GetBlobContainerClient(_options.ContainerName);
        await _containerClient.CreateIfNotExistsAsync();

        return _containerClient;
    }

    /// <summary>
    ///     Generates a public access URL (SAS) for the given blob request when supported by the container.
    /// </summary>
    /// <param name="blob">The blob request describing the target blob.</param>
    /// <param name="expiresFromNow">Optional time span for which the generated URL will be valid. Defaults to 1 day.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A URI granting temporary public read access to the blob.</returns>
    public override async Task<Uri> GetPublicAccessUrl(
        BlobRequest blob,
        TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob);
        var blobClient = client.GetBlobClient(location);

        if (!client.CanGenerateSasUri)
            throw new NotSupportedException(
                $"Current Container '{_options.ContainerName}' does not support Shared Public Access Url");

        // Create a SAS token that's valid for one day
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = client.Name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiresFromNow ?? TimeSpan.FromDays(1)) //Blob only accessible
        };

        sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
        return blobClient.GenerateSasUri(sasBuilder);
    }

    /// <summary>
    ///     Lists items in the configured container under the provided request path.
    /// </summary>
    /// <param name="blob">The blob request describing the target listing path.</param>
    /// <param name="cancellationToken">Cancellation token for the async enumeration.</param>
    /// <returns>An async stream of <see cref="BlobDetails.BlobResult" /> entries.</returns>
    public override async IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(
        BlobRequest blob,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob).RemoveHeadingSlash();
        var resultSegment = client.GetBlobsAsync(BlobTraits.None, BlobStates.All, location, cancellationToken);

        await foreach (var b in resultSegment)
            yield return new BlobDetails.BlobResult(blob.Name)
            {
                Name = blob.Name,
                Details = b.IsDirectory()
                    ? null
                    : new BlobDetails
                    {
                        ContentType = b.Properties.ContentType,
                        ContentLength = b.Properties.ContentLength!.Value,
                        CreatedOn = b.Properties.CreatedOn!.Value.LocalDateTime,
                        LastModified = b.Properties.LastModified!.Value.LocalDateTime
                    }
            };
    }

    /// <summary>
    ///     Uploads the provided blob data to the configured container and returns the saved blob name.
    /// </summary>
    /// <param name="blob">The blob payload and metadata to save.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>The name/path of the stored blob.</returns>
    public override async Task<string> SaveAsync(BlobDetails.BlobData blob,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob);
        await client.GetBlobClient(location).UploadAsync(blob.Data, blob.Overwrite, cancellationToken);
        return blob.Name;
    }

    #endregion
}