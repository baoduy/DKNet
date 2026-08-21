using DKNet.Svc.BlobStorage.AzureStorage;

namespace Svc.BlobStorage.Tests.Fixtures;

public sealed class AzureStorageBlobServiceFixture : IDisposable
{
    #region Fields

    private readonly AzuriteContainer _azureContainer;

    #endregion

    #region Constructors

    public AzureStorageBlobServiceFixture()
    {
        _azureContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.28.0")
            .WithCommand("--skipApiVersionCheck")
            .WithAutoRemove(true)
            .Build();

        _azureContainer.StartAsync().GetAwaiter().GetResult();

        Options = new AzureStorageOptions
        {
            ConnectionString = _azureContainer.GetConnectionString(),
            ContainerName = "test"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    { "BlobService:AzureStorage:ConnectionString", Options.ConnectionString },
                    { "BlobService:AzureStorage:ContainerName", Options.ContainerName }
                })
            .Build();

        var serviceCollection = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IBlobService, AzureStorageBlobService>()
            .AddAzureStorageAdapter(config);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        Service = serviceProvider.GetRequiredService<IBlobService>();
    }

    #endregion

    #region Properties

    public IBlobService Service { get; }

    /// <summary>
    ///     The options this fixture's Azurite container was configured with — exposed so tests can construct
    ///     their own <see cref="AzureStorageBlobService" /> instance directly (e.g. to exercise default options).
    /// </summary>
    public AzureStorageOptions Options { get; }

    #endregion

    #region Methods

    public void Dispose()
    {
        _azureContainer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    #endregion
}