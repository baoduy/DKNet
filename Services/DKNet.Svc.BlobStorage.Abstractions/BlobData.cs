namespace DKNet.Svc.BlobStorage.Abstractions;

public enum BlobTypes
{
    File,
    Directory,
}

public record BlobRequest(string Name)
{
    public BlobTypes Type { get; init; } = string.IsNullOrEmpty(Path.GetExtension(Name)) ? BlobTypes.Directory : BlobTypes.File;
}

public record BlobDetails
{
    public long ContentLength { get; init; }

    public required string ContentType { get; init; }

    public DateTime LastModified { get; init; }

    public DateTime CreatedOn { get; init; }
}

public record BlobResult(string Name) : BlobRequest(Name)
{
    public BlobDetails? Details { get; init; }
}

public record BlobDataResult(string Name, BinaryData Data) : BlobResult(Name);

public record BlobData(string Name, BinaryData Data) : BlobRequest(Name)
{
    public string ContentType { get; init; } = Name.GetContentTypeByExtension();
    public bool Overwrite { get; set; }
}