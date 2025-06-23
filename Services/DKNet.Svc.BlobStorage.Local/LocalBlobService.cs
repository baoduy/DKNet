using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DKNet.Svc.BlobStorage.Abstractions;

#pragma warning disable CS1998

namespace DKNet.Svc.BlobStorage.Local;

public class LocalBlobService(IOptions<LocalDirectoryOptions> options, ILogger<LocalBlobService> logger)
    : BlobService(options.Value)
{
    private readonly string _rootFolder = options.Value.RootFolder ?? $"{Directory.GetCurrentDirectory()}/LocalStore";

    private string GetFinalPath(BlobRequest blob)
    {
        var name = blob.Name;
        if (name.StartsWith('/')) name = name[1..];
        return Path.GetFullPath(Path.Combine(_rootFolder, name));
    }

    public override async Task<string> SaveAsync(BlobData blob, CancellationToken cancellationToken = default)
    {
        if (await CheckExistsAsync(blob, cancellationToken) && !blob.Overwrite)
            throw new InvalidOperationException("File already existed");

        var finalFile = GetFinalPath(blob);
        var directory = Path.GetDirectoryName(finalFile);

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);

        await File.WriteAllBytesAsync(finalFile, blob.Data.ToArray(), cancellationToken);
        return blob.Name;
    }

    public override async Task<BlobDataResult?> GetAsync(BlobRequest blob,
        CancellationToken cancellationToken = default)
    {
        var finalFile = GetFinalPath(blob);
        if (!File.Exists(finalFile)) throw new FileNotFoundException("File not found", blob.Name);
        var file = new FileInfo(finalFile);
        var data = await BinaryData.FromStreamAsync(file.OpenRead(), cancellationToken: cancellationToken);

        return new BlobDataResult(file.FullName.Replace(_rootFolder, string.Empty, StringComparison.OrdinalIgnoreCase),
            data)
        {
            Name = file.FullName.Replace(_rootFolder, string.Empty, StringComparison.OrdinalIgnoreCase),
            Type = BlobTypes.File,
            Details = new BlobDetails
            {
                ContentType = file.FullName.GetContentTypeByExtension(),
                ContentLength = file.Length,
                CreatedOn = file.CreationTime,
                LastModified = file.LastWriteTime
            }
        };
    }

    public override async IAsyncEnumerable<BlobResult> ListItemsAsync(BlobRequest blob,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var internalLocation = GetFinalPath(blob);

        if (internalLocation.IsDirectory())
        {
            var directory = new DirectoryInfo(internalLocation);

            foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return new BlobResult(file.FullName.Replace(_rootFolder, string.Empty,
                    StringComparison.OrdinalIgnoreCase))
                {
                    Type = BlobTypes.File,
                    Details = new BlobDetails
                    {
                        ContentType = file.FullName.GetContentTypeByExtension(),
                        ContentLength = file.Length,
                        CreatedOn = file.CreationTime,
                        LastModified = file.LastWriteTime
                    }
                };
            }

            foreach (var folder in directory.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new BlobResult(folder.FullName.Replace(_rootFolder, string.Empty,
                    StringComparison.OrdinalIgnoreCase));
            }
        }
        else
        {
            var file = new FileInfo(internalLocation);
            if (file.Exists)
                yield return new BlobResult(file.FullName.Replace(_rootFolder, string.Empty,
                    StringComparison.OrdinalIgnoreCase))
                {
                    Type = BlobTypes.File,
                    Details = new BlobDetails
                    {
                        ContentType = file.FullName.GetContentTypeByExtension(),
                        ContentLength = file.Length,
                        CreatedOn = file.CreationTime,
                        LastModified = file.LastWriteTime
                    }
                };
        }
    }

    public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var path = GetFinalPath(blob);
        return blob.Type == BlobTypes.File
            ? DeleteFileAsync(path, cancellationToken)
            : DeleteFolderAsync(path, cancellationToken);
    }

    private static Task<bool> DeleteFileAsync(string fileLocation, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);
        if (!File.Exists(fileLocation)) return Task.FromResult(false);
        File.Delete(fileLocation);
        return Task.FromResult(true);
    }

    private Task<bool> DeleteFolderAsync(string folderLocation, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderLocation))
        {
            logger.LogError($"The directory {folderLocation} was not found", nameof(folderLocation));
            return Task.FromResult(false);
        }

        if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);
        Directory.Delete(folderLocation, recursive: true);
        return Task.FromResult(true);
    }


    public override Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var finalFile = GetFinalPath(blob);
        return Task.FromResult(blob.Type == BlobTypes.File ? File.Exists(finalFile) : Directory.Exists(finalFile));
    }

    public override Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Public access URLs are not supported in {nameof(LocalBlobService)}. " +
            "This service is designed for local file system storage only. " +
            "Consider using a cloud-based blob storage service if you require public access functionality.");
    }
}