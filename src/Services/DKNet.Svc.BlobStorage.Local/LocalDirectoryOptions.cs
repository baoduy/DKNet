using DKNet.Svc.BlobStorage.Abstractions;

namespace DKNet.Svc.BlobStorage.Local;

public class LocalDirectoryOptions : BlobServiceOptions
{
    public static string Name
    {
        get => "BlobStorage:LocalFolder";
    }

    public string? RootFolder { get; set; }
}