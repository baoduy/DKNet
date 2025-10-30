// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: LocalBlobService.cs
// Description: Local file-system based implementation of the BlobService abstraction for development and testing.

using System.Runtime.CompilerServices;
using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CS1998

namespace DKNet.Svc.BlobStorage.Local;

/// <summary>
///     Provides local file system-based blob storage implementation.
///     This implementation stores blobs as files beneath a configured root folder and implements the
///     <see cref="IBlobService" /> contract for development and testing scenarios.
/// </summary>
/// <remarks>
///     The service intentionally does not support public access URLs. Use cloud-backed providers for public scenarios.
/// </remarks>
public class LocalBlobService(IOptions<LocalDirectoryOptions> options, ILogger<LocalBlobService> logger)
    : BlobService(options.Value)
{
    #region Fields

    private readonly string _rootFolder = options.Value.RootFolder ?? $"{Directory.GetCurrentDirectory()}/LocalStore";

    #endregion

    #region Methods

    /// <summary>
    ///     Checks whether the blob represented by <paramref name="blob" /> exists on the local file system.
    /// </summary>
    /// <param name="blob">Blob request describing the target (file or folder).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the target exists; otherwise false.</returns>
    public override Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var finalFile = GetFinalPath(blob);
        return Task.FromResult(blob.Type == BlobTypes.File ? File.Exists(finalFile) : Directory.Exists(finalFile));
    }

    /// <summary>
    ///     Create a <see cref="BlobDetails" /> instance from a <see cref="FileInfo" />.
    /// </summary>
    /// <param name="file">The file info from which to derive metadata.</param>
    /// <returns>A populated <see cref="BlobDetails" /> instance.</returns>
    private static BlobDetails CreateBlobDetails(FileInfo file) =>
        new()
        {
            ContentType = file.FullName.GetContentTypeByExtension(),
            ContentLength = file.Length,
            CreatedOn = file.CreationTime,
            LastModified = file.LastWriteTime
        };

    /// <summary>
    ///     Deletes a file or folder represented by <paramref name="blob" />.
    /// </summary>
    /// <param name="blob">Blob request describing the target.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when deletion succeeded; otherwise false.</returns>
    public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        var path = GetFinalPath(blob);
        return blob.Type == BlobTypes.File
            ? DeleteFileAsync(path, cancellationToken)
            : DeleteFolderAsync(path, cancellationToken);
    }

    /// <summary>
    ///     Deletes a single file at the specified file system path.
    /// </summary>
    /// <param name="fileLocation">Full path to the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the file was deleted; otherwise false.</returns>
    private static Task<bool> DeleteFileAsync(string fileLocation, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);

        if (!File.Exists(fileLocation)) return Task.FromResult(false);

        File.Delete(fileLocation);
        return Task.FromResult(true);
    }

    /// <summary>
    ///     Deletes a folder and its contents recursively from the local file system.
    /// </summary>
    /// <param name="folderLocation">Full path to the folder to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the folder was deleted; otherwise false.</returns>
    private Task<bool> DeleteFolderAsync(string folderLocation, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderLocation))
        {
            logger.LogError("The directory {FolderLocation} was not found", nameof(folderLocation));
            return Task.FromResult(false);
        }

        if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);

        Directory.Delete(folderLocation, true);
        return Task.FromResult(true);
    }

    /// <summary>
    ///     Retrieves blob content and metadata for the specified <paramref name="blob" /> request from the local file system.
    /// </summary>
    /// <param name="blob">The blob request describing the file to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    ///     A <see cref="BlobDetails.BlobDataResult" /> when the file exists; otherwise throws
    ///     <see cref="FileNotFoundException" />.
    /// </returns>
    public override async Task<BlobDetails.BlobDataResult?> GetAsync(
        BlobRequest blob,
        CancellationToken cancellationToken = default)
    {
        var finalFile = GetFinalPath(blob);
        if (!File.Exists(finalFile)) throw new FileNotFoundException("File not found", blob.Name);

        var file = new FileInfo(finalFile);
        var data = await BinaryData.FromStreamAsync(file.OpenRead(), cancellationToken);
        var relativePath = GetRelativePath(file.FullName);

        return new BlobDetails.BlobDataResult(relativePath, data)
        {
            Name = relativePath,
            Type = BlobTypes.File,
            Details = CreateBlobDetails(file)
        };
    }

    /// <summary>
    ///     Computes the absolute file system path for the provided <paramref name="blob" /> relative to the configured root.
    /// </summary>
    /// <param name="blob">The blob request containing the name to convert.</param>
    /// <returns>The absolute file system path.</returns>
    private string GetFinalPath(BlobRequest blob)
    {
        var name = blob.Name;
        if (name.StartsWith('/')) name = name[1..];

        return Path.GetFullPath(Path.Combine(_rootFolder, name));
    }

    /// <summary>
    ///     Public access URLs are not supported by the local file system provider and will throw.
    /// </summary>
    /// <param name="blob">The blob request to build a URL for (ignored).</param>
    /// <param name="expiresFromNow">Optional expiry window (ignored).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Never returns; always throws <see cref="NotSupportedException" />.</returns>
    public override Task<Uri> GetPublicAccessUrl(
        BlobRequest blob,
        TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"Public access URLs are not supported in {nameof(LocalBlobService)}. " +
            "This service is designed for local file system storage only. " +
            "Consider using a cloud-based blob storage service if you require public access functionality.");

    /// <summary>
    ///     Computes a relative representation of an absolute file path by removing the configured root folder prefix.
    /// </summary>
    /// <param name="fullPath">The full file system path.</param>
    /// <returns>The path relative to the configured root folder.</returns>
    private string GetRelativePath(string fullPath) =>
        fullPath.Replace(_rootFolder, string.Empty, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Lists items beneath the requested path. Yields files first and then folders encountered when listing a directory.
    /// </summary>
    /// <param name="blob">The blob request describing the target listing path.</param>
    /// <param name="cancellationToken">Cancellation token for the async enumeration.</param>
    /// <returns>An async stream of blob metadata entries.</returns>
    public override async IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(
        BlobRequest blob,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var internalLocation = GetFinalPath(blob);

        if (internalLocation.IsDirectory())
        {
            var directory = new DirectoryInfo(internalLocation);

            foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return new BlobDetails.BlobResult(GetRelativePath(file.FullName))
                {
                    Type = BlobTypes.File,
                    Details = CreateBlobDetails(file)
                };
            }

            foreach (var folder in directory.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new BlobDetails.BlobResult(GetRelativePath(folder.FullName));
            }
        }
        else
        {
            var file = new FileInfo(internalLocation);
            if (file.Exists)
                yield return new BlobDetails.BlobResult(GetRelativePath(file.FullName))
                {
                    Type = BlobTypes.File,
                    Details = CreateBlobDetails(file)
                };
        }
    }

    /// <summary>
    ///     Saves the provided blob data to the local file system, creating directories as necessary.
    /// </summary>
    /// <param name="blob">The blob payload and metadata to store.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The original blob name when the save completes.</returns>
    public override async Task<string> SaveAsync(BlobDetails.BlobData blob,
        CancellationToken cancellationToken = default)
    {
        if (await CheckExistsAsync(blob, cancellationToken) && !blob.Overwrite)
            throw new InvalidOperationException("File already existed");

        var finalFile = GetFinalPath(blob);
        var directory = Path.GetDirectoryName(finalFile);

        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);

        await File.WriteAllBytesAsync(finalFile, blob.Data.ToArray(), cancellationToken);
        return blob.Name;
    }

    #endregion
}