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