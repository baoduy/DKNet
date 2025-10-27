namespace DKNet.Svc.BlobStorage.AzureStorage;

public class AzureStorageOptions : BlobServiceOptions
{
    #region Properties

    public string ConnectionString { get; set; } = null!;
    public string ContainerName { get; set; } = null!;
    public static string Name => "BlobService:AzureStorage";

    #endregion
}