using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using DKNet.Svc.BlobStorage.Abstractions;

namespace DKNet.Svc.BlobStorage.AzureStorage;

public sealed class AzureStorageBlobService(IOptions<AzureStorageOptions> options) : BlobService(options.Value)
{
    private readonly AzureStorageOptions _options =
        options.Value ?? throw new ArgumentNullException(nameof(options));

    private BlobContainerClient? _containerClient;

    private async Task<BlobContainerClient> GetClient()
    {
        if (_containerClient != null) return _containerClient;

        var blobClient = new BlobServiceClient(_options.ConnectionString);
        _containerClient = blobClient.GetBlobContainerClient(_options.ContainerName);
        await _containerClient.CreateIfNotExistsAsync();

        return _containerClient;
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
        var client = await GetClient();
        var rs = await client.GetBlobClient(fileLocation).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return rs.Value;
    }

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
            {
                if (blob.IsDirectory())
                    queue.Enqueue(blob.Name.EnsureTrailingSlash());
                else await DeleteFileAsync(blob.Name, cancellationToken);
            }

            //Add Empty folder to stack and delete later
            subStack.Push(tbDelete);
        }

        //Delete all empty Subfolders and folder
        while (subStack.Count > 0)
        {
            await DeleteFileAsync(subStack.Pop(), cancellationToken);
        }

        //Tobe True or Exception.
        return true;
    }

    public override async Task<string> SaveAsync(BlobData blob, CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob);
        await client.GetBlobClient(location).UploadAsync(blob.Data, blob.Overwrite, cancellationToken);
        return blob.Name;
    }

    public override async Task<BlobDataResult?> GetAsync(BlobRequest blob,
        CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob);
        var b = client.GetBlobClient(location);
        var props = await b.GetPropertiesAsync(cancellationToken: cancellationToken);
        var es = await b.ExistsAsync(cancellationToken);
        if (!es.Value) return null;

        var data = await b.DownloadContentAsync(cancellationToken);
        return new BlobDataResult(blob.Name,data.Value.Content)
        {
            Type = BlobTypes.File,
            Details = new BlobDetails
            {
                ContentType = props.Value.ContentType,
                ContentLength = props.Value.ContentLength,
                CreatedOn = props.Value.CreatedOn.LocalDateTime,
                LastModified = props.Value.LastModified.LocalDateTime,
            }
        };
    }

    public override async IAsyncEnumerable<BlobResult> ListItemsAsync(BlobRequest blob,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob).RemoveHeadingSlash();
        var resultSegment = client.GetBlobsAsync(BlobTraits.None, BlobStates.All, location, cancellationToken);

        await foreach (var b in resultSegment)
        {
            yield return new BlobResult(blob.Name)
            {
                Name = blob.Name,
                Details = b.IsDirectory()
                    ? null
                    : new BlobDetails
                    {
                        ContentType = b.Properties.ContentType,
                        ContentLength = b.Properties.ContentLength!.Value,
                        CreatedOn = b.Properties.CreatedOn!.Value.LocalDateTime,
                        LastModified = b.Properties.LastModified!.Value.LocalDateTime,
                    }
            };
        }
    }


    public override async Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var client = await GetClient();
        var location = GetBlobLocation(blob);
        var rs = await client.GetBlobClient(location).ExistsAsync(cancellationToken: cancellationToken);
        return rs.Value;
    }

    public override async Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null,
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
}