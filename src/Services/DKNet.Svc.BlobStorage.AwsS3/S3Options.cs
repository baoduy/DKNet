// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: S3Options.cs
// Description: Configuration options for the S3/compatible (e.g. AWS S3, Cloudflare R2) blob storage provider.

using DKNet.Svc.BlobStorage.Abstractions;

namespace DKNet.Svc.BlobStorage.AwsS3;

/// <summary>
///     Options used to configure the S3 (or S3-compatible) blob storage provider.
///     Inherits common blob configuration from <see cref="BlobServiceOptions" /> and exposes S3-specific settings.
/// </summary>
public class S3Options : BlobServiceOptions
{
    #region Properties

    /// <summary>
    ///     Optional access key for authenticated access. When not provided the client may rely on environment/instance
    ///     credentials or other AWS SDK credential chains.
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    ///     The S3 bucket name to use for blob operations.
    /// </summary>
    public string BucketName { get; set; } = null!;

    /// <summary>
    ///     The service endpoint or connection string for the S3-compatible service. For AWS this is typically the
    ///     region endpoint; for self-hosted or compatible services this may be a full service URL.
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    ///     When true, payload signing for uploads is disabled. Useful when targeting Cloudflare R2 or other S3-compatible
    ///     endpoints that do not accept AWS payload signing.
    /// </summary>
    public bool DisablePayloadSigning { get; set; }

    /// <summary>
    ///     When true, the S3 client will use path-style addressing (http(s)://endpoint/bucket/key) instead of
    ///     virtual-hosted-style (http(s)://bucket.endpoint/key). Some S3-compatible services require this mode.
    /// </summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>
    ///     The configuration key used to bind <see cref="S3Options" /> from configuration.
    ///     Typical key: "BlobService:S3".
    /// </summary>
    public static string Name => "BlobService:S3";

    /// <summary>
    ///     Optional AWS region name (for example "ap-southeast-1", "us-east-1"). When not provided a default of
    ///     "us-east-1" is used by the implementation.
    /// </summary>
    public string? RegionEndpointName { get; set; } = "us-east-1";

    /// <summary>
    ///     Optional secret key for authenticated access. When not provided the client may rely on environment/instance
    ///     credentials or other AWS SDK credential chains.
    /// </summary>
    public string? Secret { get; set; }

    #endregion
}