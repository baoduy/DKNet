// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IBlobService.cs
// Description: Abstractions for blob storage services and a small base class with common helpers.

using System.Text;

namespace DKNet.Svc.BlobStorage.Abstractions;

/// <summary>
///     Provides an interface for blob storage operations including saving, retrieving, listing, and deleting blobs.
/// </summary>
public interface IBlobService
{
    #region Methods

    /// <summary>
    ///     Checks if a blob exists based on the provided <see cref="BlobRequest" />.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to check.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns><c>true</c> when the blob exists; otherwise <c>false</c>.</returns>
    Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a blob identified by the provided <see cref="BlobRequest" />.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns><c>true</c> when the blob was deleted; otherwise <c>false</c>.</returns>
    Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves a blob's data as <see cref="BlobDetails.BlobDataResult" />.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The blob data result when found; otherwise <c>null</c>.</returns>
    Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves metadata for a blob as <see cref="BlobDetails.BlobResult" />.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The blob item when found; otherwise <c>null</c>.</returns>
    Task<BlobDetails.BlobResult?> GetItemAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a shared (public) access URI for the specified blob.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to get the URL for.</param>
    /// <param name="expiresFromNow">Optional time span the SAS/URL should be valid for from now.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Uri" /> granting access to the blob.</returns>
    Task<Uri> GetPublicAccessUrl(
        BlobRequest blob,
        TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists blob items matching the provided <see cref="BlobRequest" /> criteria.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blobs to list.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>An async enumerable of <see cref="BlobDetails.BlobResult" /> entries.</returns>
    IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(
        BlobRequest blob,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves the provided blob data and returns a string location or identifier for the stored blob.
    /// </summary>
    /// <param name="blob">The blob data to save.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A location or identifier for the saved blob (implementation-specific).</returns>
    Task<string> SaveAsync(BlobDetails.BlobData blob, CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
///     A small abstract base class that implements common helpers for blob services. Concrete providers should
///     derive from this class and implement the abstract methods.
/// </summary>
/// <remarks>
///     Initializes a new instance of <see cref="BlobService" /> with the provided options.
/// </remarks>
/// <param name="options">Configuration options for the blob service.</param>
public abstract class BlobService(BlobServiceOptions options) : IBlobService
{
    #region Fields

    /// <summary>
    ///     Options supplied to the blob service instance.
    /// </summary>
    private readonly BlobServiceOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    #endregion

    #region Methods

    /// <summary>
    ///     Checks if the blob described by <paramref name="blob" /> exists in the backing store.
    ///     Concrete implementations must provide the mechanism for existence checks.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to check.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns><c>true</c> when the blob exists; otherwise <c>false</c>.</returns>
    public abstract Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the blob identified by <paramref name="blob" /> from the backing store.
    ///     Implementations should return <c>true</c> when deleting completed successfully or the blob did not exist.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns><c>true</c> when the blob was deleted; otherwise <c>false</c>.</returns>
    public abstract Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves the blob payload and metadata for the provided <paramref name="blob" /> request.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The blob data result when found; otherwise <c>null</c>.</returns>
    public abstract Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest blob,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Produces a normalized blob path for storage based on the provided <see cref="BlobRequest" />.
    /// </summary>
    /// <param name="item">The blob request containing the name to be normalized.</param>
    /// <returns>A normalized blob path (leading slash included).</returns>
    protected virtual string GetBlobLocation(BlobRequest item)
    {
        var builder = new StringBuilder();

        if (!item.Name.StartsWith('/')) builder.Append('/');

        builder.Append(item.Name);
        return builder.ToString();
    }

    /// <summary>
    ///     Returns the first matching item from a listing operation, or <c>null</c> if none is found.
    ///     Concrete implementations should implement <see cref="ListItemsAsync" /> to enumerate items.
    /// </summary>
    /// <param name="blob">The blob request describing which items to search for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The first matching <see cref="BlobDetails.BlobResult" />, or <c>null</c>.</returns>
    public virtual async Task<BlobDetails.BlobResult?> GetItemAsync(BlobRequest blob,
        CancellationToken cancellationToken = default)
    {
        await foreach (var item in ListItemsAsync(blob, cancellationToken)) return item;

        return null;
    }

    /// <summary>
    ///     Gets a shared (public) access URI for the specified blob.
    ///     Implementations should honor the optional <paramref name="expiresFromNow" /> value when generating short-lived
    ///     URLs.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to get the URL for.</param>
    /// <param name="expiresFromNow">Optional time span the SAS/URL should be valid for from now.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Uri" /> granting access to the blob.</returns>
    public abstract Task<Uri> GetPublicAccessUrl(
        BlobRequest blob,
        TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists blob items matching the provided <see cref="BlobRequest" /> criteria.
    ///     Implementations should stream results via the returned async enumerable.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blobs to list.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>An async enumerable of <see cref="BlobDetails.BlobResult" /> entries.</returns>
    public abstract IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(
        BlobRequest blob,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves the provided blob data and returns a location or identifier for the stored blob.
    ///     Implementations must call <see cref="ValidateFile" /> to enforce configured limits where appropriate.
    /// </summary>
    /// <param name="blob">The blob data to save.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A location or identifier for the saved blob (implementation-specific).</returns>
    public abstract Task<string> SaveAsync(BlobDetails.BlobData blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates file metadata and content against configured <see cref="BlobServiceOptions" />.
    ///     Throws <see cref="FileLoadException" /> when validation fails.
    /// </summary>
    /// <param name="item">Blob data to validate.</param>
    protected virtual void ValidateFile(BlobDetails.BlobData item)
    {
        if (_options.MaxFileNameLength > 0 && item.Name.Length > _options.MaxFileNameLength)
            throw new FileLoadException("File name is invalid.");

        var ext = Path.GetExtension(item.Name);
        if (string.IsNullOrEmpty(ext)) throw new FileLoadException("File extension is invalid.");

        if (!_options.IncludedExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
            throw new FileLoadException("File extension is invalid.");

        if (_options.MaxFileSizeInMb > 0)
        {
            var fileLength = item.Data.ToArray().LongLength;
            var limitLength = _options.MaxFileSizeInMb * 1000000; //Convert Mb to Byte
            if (fileLength > limitLength) throw new FileLoadException("File size is invalid.");
        }
    }

    #endregion
}