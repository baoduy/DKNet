namespace DKNet.Svc.BlobStorage.Abstractions;

/// <summary>
///     Provides BlobServiceOptions functionality.
/// </summary>
public class BlobServiceOptions
{
    #region Properties

    public IEnumerable<string> IncludedExtensions { get; set; } = [];

    /// <summary>
    ///     Gets or sets MaxFileNameLength.
    /// </summary>
    public int MaxFileNameLength { get; set; }

    public int MaxFileSizeInMb { get; set; }

    #endregion
}