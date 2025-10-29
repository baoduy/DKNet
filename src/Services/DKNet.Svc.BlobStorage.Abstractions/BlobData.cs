namespace DKNet.Svc.BlobStorage.Abstractions;

public enum BlobTypes
{
    File,
    Directory
}

public record BlobRequest(string Name)
{
    #region Properties

    public BlobTypes Type { get; init; } =
        string.IsNullOrEmpty(Path.GetExtension(Name)) ? BlobTypes.Directory : BlobTypes.File;

    #endregion
}

public record BlobDetails
{
    #region Properties

    public DateTime CreatedOn { get; init; }

    public DateTime LastModified { get; init; }

    public long ContentLength { get; init; }

    public required string ContentType { get; init; }

    #endregion
}

public record BlobResult(string Name) : BlobRequest(Name)
{
    #region Properties

    public BlobDetails? Details { get; init; }

    #endregion
}

public record BlobDataResult(string Name, BinaryData Data) : BlobResult(Name);

public record BlobData(string Name, BinaryData Data) : BlobRequest(Name)
{
    #region Properties

    public bool Overwrite { get; set; }

    public string ContentType { get; init; } = Name.GetContentTypeByExtension();

    #endregion
}