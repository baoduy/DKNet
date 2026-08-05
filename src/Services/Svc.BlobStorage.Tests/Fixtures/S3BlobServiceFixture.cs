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
        _minioContainer = new MinioBuilder("minio/minio:RELEASE.2023-01-31T02-24-19Z")
            .Build();

        _minioContainer.StartAsync().GetAwaiter().GetResult();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    { "BlobService:S3:ConnectionString", _minioContainer.GetConnectionString() },
                    { "BlobService:S3:AccessKey", _minioContainer.GetAccessKey() },
                    { "BlobService:S3:Secret", _minioContainer.GetSecretKey() },
                    { "BlobService:S3:BucketName", "dev" },
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

    #endregion

    #region Methods

    public void Dispose()
    {
        _minioContainer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    #endregion
}