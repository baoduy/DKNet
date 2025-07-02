using DKNet.Svc.BlobStorage.Abstractions;

namespace DKNet.Svc.BlobStorage.AwsS3;

public class S3Options : BlobServiceOptions
{
    public static string Name => "BlobService:S3";

    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// The Name of RegionEnd ex: ap-southeast-1, ap-east-1 or ca-central-1 ...
    /// </summary>
    public string? RegionEndpointName { get; set; } = "us-east-1";

    public string BucketName { get; set; } = null!;

    public string? AccessKey { get; set; }
    public string? Secret { get; set; }

    /// <summary>
    /// If using this service with Cloudflare R2, set this to true.
    /// </summary>
    public bool DisablePayloadSigning { get; set; }

    public bool ForcePathStyle { get; set; }
}