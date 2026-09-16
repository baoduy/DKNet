namespace Svc.BlobStorage.Tests.Fixtures;

public sealed class LocalBlobServiceFixture : IDisposable
{
    #region Constructors

    public LocalBlobServiceFixture()
    {
        TestRoot = Path.Combine(Path.GetTempPath(), "DKNet-LocalBlob-", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(TestRoot);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    { "BlobStorage:LocalFolder:RootFolder", TestRoot }
                })
            .Build();

        var serviceCollection = new ServiceCollection()
            .AddLogging()
            .AddLocalDirectoryBlobService(config);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        Service = serviceProvider.GetRequiredService<IBlobService>();
    }

    #endregion

    #region Properties

    public IBlobService Service { get; }

    public string TestRoot { get; }

    #endregion

    #region Methods

    public void Dispose()
    {
        if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, true);

        GC.SuppressFinalize(this);
    }

    #endregion
}