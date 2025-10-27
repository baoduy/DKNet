using System.Text;

namespace DKNet.Svc.BlobStorage.Abstractions;

/// <summary>
///     Provides an interface for blob storage operations including saving, retrieving, listing, and deleting blobs.
/// </summary>
public interface IBlobService
{
    #region Methods

    /// <summary>
    ///     Checks if a blob exists based on blob arguments.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to check.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>True if the blob exists; otherwise, false.</returns>
    Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a blob based on blob arguments.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>True if the blob was deleted; otherwise, false.</returns>
    Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves a blob's data based on blob arguments.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The blob's data if found; otherwise, null.</returns>
    Task<BlobDataResult?> GetAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves a blob item's metadata based on blob arguments.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The blob item if found; otherwise, null.</returns>
    Task<BlobResult?> GetItemAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a Shared access URL for a blob based on blob arguments.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blob to get the URL for.</param>
    /// <param name="expiresFromNow"></param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A URI that provides public access to the blob.</returns>
    Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists blob items based on blob arguments.
    /// </summary>
    /// <param name="blob">The blob arguments specifying which blobs to list.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>An async enumerable of blob items.</returns>
    IAsyncEnumerable<BlobResult> ListItemsAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves a blob with the specified data and returns its location.
    /// </summary>
    /// <param name="blob">The blob data to save.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The location where the blob was saved.</returns>
    Task<string> SaveAsync(BlobData blob, CancellationToken cancellationToken = default);

    #endregion
}

public abstract class BlobService(BlobServiceOptions options) : IBlobService
{
    #region Methods

    public abstract Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    public abstract Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    public abstract Task<BlobDataResult?> GetAsync(BlobRequest blob, CancellationToken cancellationToken = default);

    protected virtual string GetBlobLocation(BlobRequest item)
    {
        var builder = new StringBuilder();

        if (!item.Name.StartsWith('/'))
            builder.Append('/');

        builder.Append(item.Name);
        return builder.ToString();
    }

    public virtual async Task<BlobResult?> GetItemAsync(BlobRequest blob, CancellationToken cancellationToken = default)
    {
        await foreach (var item in ListItemsAsync(blob, cancellationToken))
            return item;
        return null;
    }

    public abstract Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<BlobResult> ListItemsAsync(BlobRequest blob,
        CancellationToken cancellationToken = default);

    public abstract Task<string> SaveAsync(BlobData blob, CancellationToken cancellationToken = default);

    protected virtual void ValidateFile(BlobData item)
    {
        if (options.MaxFileNameLength > 0 && item.Name.Length > options.MaxFileNameLength)
            throw new FileLoadException("File name is invalid.");

        var ext = Path.GetExtension(item.Name);
        if (string.IsNullOrEmpty(ext))
            throw new FileLoadException("File extension is invalid.");

        if (!options.IncludedExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
            throw new FileLoadException("File extension is invalid.");

        if (options.MaxFileSizeInMb > 0)
        {
            var fileLength = item.Data.ToArray().LongLength;
            var limitLength = options.MaxFileSizeInMb * 1000000; //Convert Mb to Byte
            if (fileLength > limitLength)
                throw new FileLoadException("File size is invalid.");
        }
    }

    #endregion
}