using DKNet.Svc.BlobStorage.Abstractions;

namespace DKNet.Svc.BlobStorage.Local;

public class LocalDirectoryOptions : BlobServiceOptions
{
    public static string Name => "BlobStorage:LocalFolder";

    public string? RootFolder { get; set; }
}