using DKNet.Svc.BlobStorage.AzureStorage;

namespace Svc.BlobStorage.Tests.Fixtures;

public sealed class AzureStorageBlobServiceFixture : IDisposable
{
    private readonly AzuriteContainer _azureContainer;

    public AzureStorageBlobServiceFixture()
    {
        _azureContainer = new AzuriteBuilder()
            .WithCommand("--skipApiVersionCheck")
            .WithPortBinding(10000)
            .WithAutoRemove(true)
            .Build();

        _azureContainer.StartAsync().GetAwaiter().GetResult();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                { "BlobService:AzureStorage:ConnectionString", "UseDevelopmentStorage=true" },
                { "BlobService:AzureStorage:ContainerName", "test" }
            })
            .Build();

        var serviceCollection = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IBlobService, AzureStorageBlobService>()
            .AddAzureStorageAdapter(config);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        Service = serviceProvider.GetRequiredService<IBlobService>();
    }

    public IBlobService Service { get; }

    public void Dispose()
    {
        _azureContainer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}