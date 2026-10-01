using DKNet.Svc.BlobStorage.AwsS3;
using Testcontainers.Minio;

namespace Svc.BlobStorage.Tests.Fixtures;

public sealed class S3BlobServiceFixture : IDisposable
{
    #region Fields

    private readonly MinioContainer _minioContainer;

    #endregion

    #region Constructors

    public S3BlobServiceFixture()
    {
        // minio/minio was withdrawn from Docker Hub, then quay.io/minio/minio withdrew anonymous
        // pulls too (401 on both the pinned digest and latest, 2026-10-01). pgsty/minio is
        // Pigsty's independent "Silo" fork of MinIO (RELEASE.2026-08-04T00-00-00Z, built on
        // upstream MinIO RELEASE.2025-12-03T12-00-00Z), not a mirror of the withdrawing publisher
        // — Pigsty ships and controls its own release line, so MinIO Inc.'s access decisions on
        // Docker Hub/quay.io can't pull this source out from under us the same way again. Pulled
        // here anonymously and pinned by the manifest-list digest so a future re-tag can't
        // silently change what this suite runs against; the digest resolves to a multi-arch index
        // covering linux/amd64 and linux/arm64.
        _minioContainer = new MinioBuilder(
                "pgsty/minio@sha256:b6bfe7239bfc83fb90d31612d9704d86039dd714f7904b3f1ad68f211e602372")
            .Build();

        _minioContainer.StartAsync().GetAwaiter().GetResult();

        Options = new S3Options
        {
            ConnectionString = _minioContainer.GetConnectionString(),
            AccessKey = _minioContainer.GetAccessKey(),
            Secret = _minioContainer.GetSecretKey(),
            BucketName = "dev",
            DisablePayloadSigning = false,
            ForcePathStyle = true
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    { "BlobService:S3:ConnectionString", Options.ConnectionString },
                    { "BlobService:S3:AccessKey", Options.AccessKey },
                    { "BlobService:S3:Secret", Options.Secret },
                    { "BlobService:S3:BucketName", Options.BucketName },
                    { "BlobService:S3:DisablePayloadSigning", "false" },
                    { "BlobService:S3:ForcePathStyle", "true" }
                })
            .Build();

        var serviceCollection = new ServiceCollection()
            .AddLogging()
            .AddS3BlobService(config);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        Service = serviceProvider.GetRequiredService<IBlobService>();
    }

    #endregion

    #region Properties

    public IBlobService Service { get; }

    /// <summary>
    ///     The options this fixture's Minio container was configured with — exposed so tests can construct
    ///     their own <see cref="S3BlobService" /> instance directly (e.g. to exercise Dispose()).
    /// </summary>
    public S3Options Options { get; }

    #endregion

    #region Methods

    public void Dispose()
    {
        _minioContainer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    #endregion
}