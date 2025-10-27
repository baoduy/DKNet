namespace DKNet.Svc.BlobStorage.Abstractions;

public class BlobServiceOptions
{
    #region Properties

    public IEnumerable<string> IncludedExtensions { get; set; } = [];

    public int MaxFileNameLength { get; set; }

    public int MaxFileSizeInMb { get; set; }

    #endregion
}