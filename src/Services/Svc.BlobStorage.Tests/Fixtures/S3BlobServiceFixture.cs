using DKNet.Svc.BlobStorage.AwsS3;
using Testcontainers.Minio;

namespace Svc.BlobStorage.Tests.Fixtures;

public sealed class S3BlobServiceFixture : IDisposable
{
    private readonly MinioContainer _minioContainer;

    public S3BlobServiceFixture()
    {
        _minioContainer = new MinioBuilder()
            .Build();
        _minioContainer.StartAsync().GetAwaiter().GetResult();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                (StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "BlobService:S3:ConnectionString",
                        "https://c4bf6253a59daf70a445861c23b45778.r2.cloudflarestorage.com"
                    },
                    { "BlobService:S3:AccessKey", "c5240e9de9fb8f2b24d67315eed90737" },
                    { "BlobService:S3:Secret", "df8dc0fe841d98c8c8429e3fbe5a6e0e784865e835860b4ffeb65913d7e7346b" },
                    { "BlobService:S3:BucketName", "dev" },
                    { "BlobService:S3:DisablePayloadSigning", "true" }
                })
            .Build();

        var serviceCollection = new ServiceCollection()
            .AddLogging()
            .AddS3BlobService(config);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        Service = serviceProvider.GetRequiredService<IBlobService>();
    }

    public IBlobService Service { get; }

    public void Dispose()
    {
        _minioContainer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}