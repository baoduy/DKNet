using System.Runtime.CompilerServices;
using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CS1998

namespace DKNet.Svc.BlobStorage.Local;

/// <summary>
///     Provides local file system-based blob storage implementation.
/// </summary>
/// <remarks>
///     Purpose: To provide blob storage capabilities using the local file system.
///     Rationale: Enables blob storage functionality for development, testing, or scenarios where cloud storage is not
///     required.
///     Functionality:
///     - Stores blobs as files in a local directory structure
///     - Supports directory-based organization
///     - Provides file metadata through BlobDetails
///     - Implements all IBlobService operations except public URL generation
///     Integration:
///     - Implements IBlobService for compatibility with other blob storage providers
///     - Uses LocalDirectoryOptions for configuration
///     - Leverages standard .NET file I/O operations
///     Best Practices:
///     - Ensure the root folder has appropriate read/write permissions
///     - Consider file system limitations when storing large numbers of files
///     - Use cloud storage for production scenarios requiring public access
///     - Monitor disk space usage in production environments
/// </remarks>
public class LocalBlobService(IOptions<LocalDirectoryOptions> options, ILogger<LocalBlobService> logger)
    : BlobService(options.Value)
{
    #region Fields

    private readonly string _rootFolder = options.Value.RootFolder ?? $"{Directory.GetCurrentDirectory()}/LocalStore";

    #endregion

    #region Methods

    public override Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var finalFile = this.GetFinalPath(blob);
        return Task.FromResult(blob.Type == BlobTypes.File ? File.Exists(finalFile) : Directory.Exists(finalFile));
    }

    private static BlobDetails CreateBlobDetails(FileInfo file) =>
        new()
        {
            ContentType = file.FullName.GetContentTypeByExtension(),
            ContentLength = file.Length,
            CreatedOn = file.CreationTime,
            LastModified = file.LastWriteTime
        };

    public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var path = this.GetFinalPath(blob);
        return blob.Type == BlobTypes.File
            ? DeleteFileAsync(path, cancellationToken)
            : this.DeleteFolderAsync(path, cancellationToken);
    }

    private static Task<bool> DeleteFileAsync(string fileLocation, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        if (!File.Exists(fileLocation))
        {
            return Task.FromResult(false);
        }

        File.Delete(fileLocation);
        return Task.FromResult(true);
    }

    private Task<bool> DeleteFolderAsync(string folderLocation, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderLocation))
        {
            logger.LogError("The directory {FolderLocation} was not found", nameof(folderLocation));
            return Task.FromResult(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        Directory.Delete(folderLocation, true);
        return Task.FromResult(true);
    }

    public override async Task<BlobDataResult?> GetAsync(
        BlobRequest blob,
        CancellationToken cancellationToken = default)
    {
        var finalFile = this.GetFinalPath(blob);
        if (!File.Exists(finalFile))
        {
            throw new FileNotFoundException("File not found", blob.Name);
        }

        var file = new FileInfo(finalFile);
        var data = await BinaryData.FromStreamAsync(file.OpenRead(), cancellationToken);
        var relativePath = this.GetRelativePath(file.FullName);

        return new BlobDataResult(relativePath, data)
        {
            Name = relativePath,
            Type = BlobTypes.File,
            Details = CreateBlobDetails(file)
        };
    }

    private string GetFinalPath(BlobRequest blob)
    {
        var name = blob.Name;
        if (name.StartsWith('/'))
        {
            name = name[1..];
        }

        return Path.GetFullPath(Path.Combine(this._rootFolder, name));
    }

    public override Task<Uri> GetPublicAccessUrl(
        BlobRequest blob,
        TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"Public access URLs are not supported in {nameof(LocalBlobService)}. " +
            "This service is designed for local file system storage only. " +
            "Consider using a cloud-based blob storage service if you require public access functionality.");

    private string GetRelativePath(string fullPath) =>
        fullPath.Replace(this._rootFolder, string.Empty, StringComparison.OrdinalIgnoreCase);

    public override async IAsyncEnumerable<BlobResult> ListItemsAsync(
        BlobRequest blob,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var internalLocation = this.GetFinalPath(blob);

        if (internalLocation.IsDirectory())
        {
            var directory = new DirectoryInfo(internalLocation);

            foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return new BlobResult(this.GetRelativePath(file.FullName))
                {
                    Type = BlobTypes.File,
                    Details = CreateBlobDetails(file)
                };
            }

            foreach (var folder in directory.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new BlobResult(this.GetRelativePath(folder.FullName));
            }
        }
        else
        {
            var file = new FileInfo(internalLocation);
            if (file.Exists)
            {
                yield return new BlobResult(this.GetRelativePath(file.FullName))
                {
                    Type = BlobTypes.File,
                    Details = CreateBlobDetails(file)
                };
            }
        }
    }

    public override async Task<string> SaveAsync(BlobData blob, CancellationToken cancellationToken = default)
    {
        if (await this.CheckExistsAsync(blob, cancellationToken) && !blob.Overwrite)
        {
            throw new InvalidOperationException("File already existed");
        }

        var finalFile = this.GetFinalPath(blob);
        var directory = Path.GetDirectoryName(finalFile);

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        await File.WriteAllBytesAsync(finalFile, blob.Data.ToArray(), cancellationToken);
        return blob.Name;
    }

    #endregion
}