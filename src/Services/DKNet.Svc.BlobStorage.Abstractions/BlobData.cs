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
/// <summary>
///     BlobRequest operation.
/// </summary>
/// <param name="Name">The Name parameter.</param>
/// <returns>The result of the operation.</returns>
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
/// <summary>
///     BlobResult operation.
/// </summary>
/// <param name="Name">The Name parameter.</param>
/// <returns>The result of the operation.</returns>
public record BlobResult(string Name) : BlobRequest(Name)
{
    #region Properties

    public BlobDetails? Details { get; init; }

    #endregion
}

/// <summary>
///     BlobData operation.
/// </summary>
/// <param name="Name">The Name parameter.</param>
/// <param name="Data">The Data parameter.</param>
/// <returns>The result of the operation.</returns>
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