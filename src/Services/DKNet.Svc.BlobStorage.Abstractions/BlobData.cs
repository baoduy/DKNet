namespace DKNet.Svc.BlobStorage.Abstractions;

/// <summary>
///     Defines BlobTypes values.
/// </summary>
public enum BlobTypes
{
    File,
    Directory
}

/// <summary>
///     BlobRequest operation.
/// </summary>
public record BlobRequest(string Name)
{
    #region Properties

    /// <summary>
    ///     Gets or sets Type.
    /// </summary>
    public BlobTypes Type { get; init; } =
        string.IsNullOrEmpty(Path.GetExtension(Name)) ? BlobTypes.Directory : BlobTypes.File;

    #endregion
}

public record BlobDetails
{
    #region Properties

    /// <summary>
    ///     Gets or sets CreatedOn.
    /// </summary>
    public DateTime CreatedOn { get; init; }

    /// <summary>
    ///     Gets or sets LastModified.
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    ///     Gets or sets ContentLength.
    /// </summary>
    public long ContentLength { get; init; }

    public required string ContentType { get; init; }

    #endregion
}

/// <summary>
///     BlobResult operation.
/// </summary>
public record BlobResult(string Name) : BlobRequest(Name)
{
    #region Properties

    public BlobDetails? Details { get; init; }

    #endregion
}

public record BlobDataResult(string Name, BinaryData Data) : BlobResult(Name);

/// <summary>
///     BlobData operation.
/// </summary>
public record BlobData(string Name, BinaryData Data) : BlobRequest(Name)
{
    #region Properties

    /// <summary>
    ///     Gets or sets Overwrite.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    ///     Gets or sets ContentType.
    /// </summary>
    public string ContentType { get; init; } = Name.GetContentTypeByExtension();

    #endregion
}