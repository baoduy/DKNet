using DKNet.Svc.BlobStorage.Abstractions;

namespace DKNet.Svc.BlobStorage.AwsS3;

public class S3Options : BlobServiceOptions
{
    #region Properties

    /// <summary>
    ///     If using this service with Cloudflare R2, set this to true.
    /// </summary>
    public bool DisablePayloadSigning { get; set; }

    public bool ForcePathStyle { get; set; }

    public string BucketName { get; set; } = null!;

    public string ConnectionString { get; set; } = null!;

    public static string Name => "BlobService:S3";

    public string? AccessKey { get; set; }

    /// <summary>
    ///     The Name of RegionEnd ex: ap-southeast-1, ap-east-1 or ca-central-1 ...
    /// </summary>
    public string? RegionEndpointName { get; set; } = "us-east-1";

    public string? Secret { get; set; }

    #endregion
}