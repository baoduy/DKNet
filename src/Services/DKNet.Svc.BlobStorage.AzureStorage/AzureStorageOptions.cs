using DKNet.Svc.BlobStorage.Abstractions;

namespace DKNet.Svc.BlobStorage.AzureStorage;

public class AzureStorageOptions:BlobServiceOptions
{
    public static string Name => "BlobService:AzureStorage";

    public string ConnectionString { get; set; } = null!;
    public string ContainerName { get; set; } = null!;
}