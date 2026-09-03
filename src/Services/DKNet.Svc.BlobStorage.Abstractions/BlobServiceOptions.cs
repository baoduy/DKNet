namespace DKNet.Svc.BlobStorage.Abstractions;

/// <summary>
///     Provides BlobServiceOptions functionality.
/// </summary>
public class BlobServiceOptions
{
    #region Properties

    /// <summary>
    /// </summary>
    public IReadOnlyList<string> IncludedExtensions { get; set; } = [];

    /// <summary>
    ///     Gets or sets MaxFileNameLength.
    /// </summary>
    public int MaxFileNameLength { get; set; }

    /// <summary>
    /// </summary>
    public int MaxFileSizeInMb { get; set; }

    #endregion
}